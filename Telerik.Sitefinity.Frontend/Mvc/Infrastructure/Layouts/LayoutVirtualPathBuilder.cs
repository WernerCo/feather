﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Hosting;
using Telerik.Sitefinity.Abstractions.VirtualPath;
using Telerik.Sitefinity.Frontend.Resources;
using Telerik.Sitefinity.Services;

namespace Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Layouts
{
    /// <summary>
    /// This class is responsible for resolving virtual paths for the layout templates. 
    /// </summary>
    internal class LayoutVirtualPathBuilder
    {
        #region Public members

        /// <summary>
        /// Builds the path from template title.
        /// </summary>
        /// <param name="templateTitle">Title of the template.</param>
        /// <returns> Resolved path will be in the following format: "~/SfLayouts/some_title.master"</returns>
        public virtual string BuildPathFromTitle(string templateTitle)
        {
            var templateFileNameParser = new TemplateTitleParser();
            var fileName = templateFileNameParser.GetLayoutName(templateTitle);

            var layoutVirtualPath = string.Format(CultureInfo.InvariantCulture, LayoutVirtualPathBuilder.LayoutVirtualPathTemplate, LayoutVirtualPathBuilder.LayoutsPrefix, fileName, LayoutVirtualPathBuilder.LayoutSuffix);
            layoutVirtualPath = this.AddVariablesToPath(layoutVirtualPath);

            return layoutVirtualPath;
        }

        /// <summary>
        /// Gets the layout file name from virtual path.
        /// </summary>
        /// <param name="definition">The definition.</param>
        /// <param name="virtualPath">The virtual path.</param>
        /// <returns></returns>
        public virtual string GetLayoutName(PathDefinition definition, string virtualPath)
        {
            if (!virtualPath.EndsWith(LayoutVirtualPathBuilder.LayoutSuffix, StringComparison.OrdinalIgnoreCase))
                return null;

            var definitionVp = VirtualPathUtility.AppendTrailingSlash(VirtualPathUtility.ToAppRelative(definition.VirtualPath));
            var pageTemplateNameLength = virtualPath.Length - definitionVp.Length - LayoutVirtualPathBuilder.LayoutSuffix.Length - 1;
            string pageTemplateName = virtualPath.Substring(definitionVp.Length, pageTemplateNameLength);

            while (!string.IsNullOrEmpty(pageTemplateName) && pageTemplateName.EndsWith(".", StringComparison.Ordinal))
            {
                pageTemplateName = pageTemplateName.Substring(0, pageTemplateName.Length - 1);
            }

            return pageTemplateName;
        }

        #endregion

        #region Protected and Private methods

        /// <summary>
        /// Adds variable parameters to the virtual path to allow caching of multiple versions based on parameters.
        /// </summary>
        /// <param name="layoutVirtualPath">The layout virtual path.</param>
        /// <returns>The path with appended variables.</returns>
        protected virtual string AddVariablesToPath(string layoutVirtualPath)
        {
            var varies = new List<string>();

            var packagesManager = new PackageManager();
            var currentPackage = packagesManager.GetCurrentPackage();
            if (!currentPackage.IsNullOrEmpty())
                varies.Add(currentPackage);

            if (SystemManager.CurrentContext.AppSettings.Multilingual)
                varies.Add(CultureInfo.CurrentUICulture.Name);

            if (MasterPageBuilder.IsFormTagRequired())
                varies.Add("form");
            else
                varies.Add("noform");

            var pageData = MasterPageBuilder.GetRequestedPageData();
            if (pageData != null)
            {
                varies.Add(pageData.Id.ToString());
                varies.Add(pageData.Version.ToString(CultureInfo.InvariantCulture));
            }

            layoutVirtualPath = (new VirtualPathBuilder()).AddParams(layoutVirtualPath, string.Join("_", varies));

            return layoutVirtualPath;
        }

        #endregion

        #region Constants

        /// <summary>
        /// The layouts prefix.
        /// </summary>
        public const string LayoutsPrefix = "SfLayouts";

        /// <summary>
        /// This suffix is recognized by the VirtualPathResolver for resolving the layout page.
        /// </summary>
        public const string LayoutSuffix = "master";

        /// <summary>
        /// The template used when resolving the layout virtual path. 
        /// </summary>
        private const string LayoutVirtualPathTemplate = "~/{0}/{1}.{2}"; 

        #endregion
    }
}
