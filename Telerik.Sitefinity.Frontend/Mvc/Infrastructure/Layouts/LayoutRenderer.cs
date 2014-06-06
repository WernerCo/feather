﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Web;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Routing;
using Telerik.Sitefinity.Frontend.Mvc.Controllers;
using Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Controllers;
using Telerik.Sitefinity.Frontend.Resources;
using Telerik.Sitefinity.Services;

namespace Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Layouts
{
    /// <summary>
    /// This class builds the layout master page by using the Mvc view engines.
    /// </summary>
    public class LayoutRenderer
    {
        #region Public methods

        /// <summary>
        /// Creates a controller instance and sets its ControllerContext depending on the current Http context.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="routeData">The route data.</param>
        /// <param name="context">The context.</param>
        /// <returns></returns>
        /// <exception cref="System.InvalidOperationException">Can't create Controller Context if no active HttpContext instance is available.</exception>
        public virtual Controller CreateController(RouteData routeData = null)
        {
            // create a disconnected controller instance
            GenericController controller = new GenericController();
            HttpContextBase context = null;

            // get context wrapper from HttpContext if available
            if (SystemManager.CurrentHttpContext != null)
                context = SystemManager.CurrentHttpContext;
            else
                throw new InvalidOperationException("Can't create ControllerContext if no active HttpContext instance is available.");

            if (routeData == null)
                routeData = new RouteData();

            // add the controller routing if not existing
            if (!routeData.Values.ContainsKey("controller") &&
                !routeData.Values.ContainsKey("Controller"))
                routeData.Values.Add("controller",
                                     controller.GetType().Name.ToLower().Replace("controller", ""));

            controller.UpdateViewEnginesCollection(this.GetPathTransformations(controller));

            //here we create the context for the controller passing the just created controller the httpcontext and the route data that we built above
            controller.ControllerContext = new ControllerContext(context, routeData, controller);

            return controller;
        }

        /// <summary>
        /// Renders the view to string.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="viewPath">The view path.</param>
        /// <param name="model">The model.</param>
        /// <param name="partial">if set to <c>true</c> the view should be partial.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException">View cannot be found.</exception>
        public virtual string RenderViewToString(ControllerContext context, string viewPath, object model = null, bool partial = false)
        {
            string result = null;
            IView view = null;

            //Obtaining the view engine result
            var viewEngineResult = this.GetViewEngineResult(context, viewPath, partial);

            if (viewEngineResult != null)
                view = viewEngineResult.View;

            if (view != null)
            {
                //assigning the model so it can be available to the view 
                context.Controller.ViewData.Model = model;

                using (var writer = new StringWriter())
                {
                    var viewConext = new ViewContext(context, view, context.Controller.ViewData, context.Controller.TempData, writer);
                    view.Render(viewConext, writer);

                    //the view must be released
                    viewEngineResult.ViewEngine.ReleaseView(context, view);
                    result = writer.ToString();
                }

                //Add cache dependency on the virtual file that is used for rendering the view.
                var httpContext = SystemManager.CurrentHttpContext;
                var builtView = view as BuildManagerCompiledView;
                if (httpContext != null && builtView != null)
                {
                    var vpDependency = HostingEnvironment.VirtualPathProvider != null ?
                        HostingEnvironment.VirtualPathProvider.GetCacheDependency(builtView.ViewPath, null, DateTime.UtcNow) : null;
                    if (vpDependency != null)
                    {
                        httpContext.Response.AddCacheDependency(vpDependency);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the view engine result.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <param name="viewPath">The view path.</param>
        /// <param name="partial">if set to <c>true</c> method returns only partial views.</param>
        /// <returns></returns>
        /// <exception cref="System.IO.FileNotFoundException">View cannot be found.</exception>
        public virtual ViewEngineResult GetViewEngineResult(ControllerContext context, string viewPath, bool partial = false)
        {
            var controller = (Controller)context.Controller;
            ViewEngineResult viewEngineResult;

            if (partial)
                viewEngineResult = controller.ViewEngineCollection.FindPartialView(context, viewPath);
            else
                viewEngineResult = controller.ViewEngineCollection.FindView(context, viewPath, null);

            if (viewEngineResult == null)
                throw new FileNotFoundException("View cannot be found.");

            return viewEngineResult;
        }

        #endregion

        #region ILayoutTemplateBuilder implementation

        /// <summary>
        /// Gets the layout template.
        /// </summary>
        /// <param name="templateName"></param>
        /// <param name="model">The model.</param>
        /// <param name="partial">if set to <c>true</c> requested view is partial.</param>
        /// <returns></returns>
        public virtual string GetLayoutTemplate(string templateName, object model = null, bool partial = false)
        {
            var genericController = this.CreateController();
            var layoutHtmlString = this.RenderViewToString(genericController.ControllerContext, templateName, model, partial);

            if (!layoutHtmlString.IsNullOrEmpty())
            {
                var htmlProcessor = new MasterPageBuilder();
                layoutHtmlString = htmlProcessor.ProcessLayoutString(layoutHtmlString);
                layoutHtmlString = htmlProcessor.AddMasterPageDirectives(layoutHtmlString);
            }

            return layoutHtmlString;
        }

        /// <summary>
        /// Determines whether a layout with specified name exists.
        /// </summary>
        /// <param name="templateName"></param>
        /// <param name="partial">if set to <c>true</c> the result view should be partial.</param>
        /// <returns></returns>
        public bool LayoutExists(string templateName, bool partial = false)
        {
            var genericController = this.CreateController();
            var viewEngineResult = this.GetViewEngineResult(genericController.ControllerContext, templateName, partial);
            bool result = viewEngineResult != null && viewEngineResult.View != null;

            if (result)
                viewEngineResult.ViewEngine.ReleaseView(genericController.ControllerContext, viewEngineResult.View);

            return result;
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Gets the path transformations.
        /// </summary>
        /// <param name="controller">The controller.</param>
        /// <returns></returns>
        private IList<Func<string, string>> GetPathTransformations(Controller controller)
        {
            var packagesManager = new PackageManager();

            var currentPackage = packagesManager.GetCurrentPackage();
            var pathTransformations = new List<Func<string, string>>(1);
            var baseVirtualPath = FrontendManager.VirtualPathBuilder.GetVirtualPath(this.GetType().Assembly);

            pathTransformations.Add(path =>
                {
                    //{1} is the ControllerName argument in VirtualPathProviderViewEngines
                    var result = path
                                    .Replace("{1}", "Layouts")
                                    .Replace("~/", "~/{0}Mvc/".Arrange(baseVirtualPath));

                    if (!currentPackage.IsNullOrEmpty())
                        result += "#" + currentPackage + Path.GetExtension(path);

                    return result;
                });

            return pathTransformations;
        }

        #endregion
    }
}
