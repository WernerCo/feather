﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Web.Hosting;
using System.Web.Mvc;
using System.Web.Routing;
using Telerik.Microsoft.Practices.Unity;
using Telerik.Sitefinity.Abstractions;
using Telerik.Sitefinity.Abstractions.VirtualPath;
using Telerik.Sitefinity.Configuration;
using Telerik.Sitefinity.Frontend.Mvc.Controllers;
using Telerik.Sitefinity.Frontend.Mvc.Infrastructure;
using Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Controllers.Attributes;
using Telerik.Sitefinity.Frontend.Resources;
using Telerik.Sitefinity.Frontend.Resources.Resolvers;
using Telerik.Sitefinity.Localization;
using Telerik.Sitefinity.Mvc;
using Telerik.Sitefinity.Mvc.Store;
using Telerik.Sitefinity.Services;

namespace Telerik.Sitefinity.Frontend.Mvc.Infrastructure.Controllers
{
    /// <summary>
    /// This class contains logic for locating and initializing controllers.
    /// </summary>
    public class ControllerContainerInitializer
    {
        #region Public members

        /// <summary>
        /// Initializes the controllers that are available to the web application.
        /// </summary>
        public virtual void Initialize()
        {
            GlobalFilters.Filters.Add(new CacheDependentAttribute());

            var assemblies = this.GetAssemblies();

            this.RegisterVirtualPaths(assemblies);

            var controllerTypes = this.GetControllers(assemblies);
            this.InitializeControllers(controllerTypes);
        }

        #endregion

        #region Protected members

        /// <summary>
        /// Gets the assemblies that are marked as controller containers with the <see cref="ControllerContainerAttribute"/> attribute.
        /// </summary>
        protected virtual IEnumerable<Assembly> GetAssemblies()
        {
            var assemblyFileNames = this.GetAssemblyFileNames().ToArray();
            var result = new List<Assembly>();

            foreach (var assemblyFileName in assemblyFileNames)
            {
                if (this.IsControllerContainer(assemblyFileName))
                {
                    var assembly = this.LoadAssembly(assemblyFileName);
                    this.InitializeControllerContainer(assembly);
                    result.Add(assembly);
                }
            }

            return result;
        }

        /// <summary>
        /// Executes the initialization method specified in the <see cref="ControllerContainerAttribute"/> attribute.
        /// </summary>
        /// <param name="container"></param>
        protected virtual void InitializeControllerContainer(Assembly container)
        {
            var containerAttribute = container.GetCustomAttributes(false).Single(attr => attr.GetType().AssemblyQualifiedName == typeof(ControllerContainerAttribute).AssemblyQualifiedName) as ControllerContainerAttribute;

            if (containerAttribute.InitializationType == null || containerAttribute.InitializationMethod.IsNullOrWhitespace())
                return;

            var initializationMethod = containerAttribute.InitializationType.GetMethod(containerAttribute.InitializationMethod);
            initializationMethod.Invoke(null, null);
        }

        /// <summary>
        /// Loads the assembly file into the current application domain.
        /// </summary>
        /// <param name="assemblyFileName">Name of the assembly file.</param>
        /// <returns>The loaded assembly</returns>
        protected virtual Assembly LoadAssembly(string assemblyFileName)
        {
            return Assembly.LoadFrom(assemblyFileName);
        }

        /// <summary>
        /// Registers virtual paths for the given <paramref name="assemblies"/>.
        /// </summary>
        /// <param name="assemblies">The assemblies.</param>
        protected virtual void RegisterVirtualPaths(IEnumerable<Assembly> assemblies)
        {
            foreach (var assembly in assemblies)
            {
                this.RegisterAssemblyPaths(assembly);
            }
        }

        /// <summary>
        /// Gets the controllers from the given <paramref name="assemblies"/>.
        /// </summary>
        /// <param name="assemblies">The assemblies.</param>
        /// <returns></returns>
        protected virtual IEnumerable<Type> GetControllers(IEnumerable<Assembly> assemblies)
        {
            return assemblies
                .SelectMany(asm => asm.GetExportedTypes().Where(FrontendManager.ControllerFactory.IsController))
                .ToArray();
        }

        /// <summary>
        /// Initializes the specified <paramref name="controllers"/> by ensuring they have their proper registrations in the toolbox and that the controller factory will be able to resolve them.
        /// </summary>
        /// <param name="controllers">The controllers.</param>
        protected virtual void InitializeControllers(IEnumerable<Type> controllers)
        {
            this.RegisterControllerFactory();
            this.RemoveSitefinityViewEngine();
            this.ReplaceControllerFactory();

            foreach (var controller in controllers)
            {
                this.RegisterController(controller);
            }
        }

        /// <summary>
        /// Registers <see cref="FrontendControllerFactory"/>
        /// </summary>
        protected virtual void RegisterControllerFactory()
        {
            ObjectFactory.Container.RegisterType<ISitefinityControllerFactory, FrontendControllerFactory>(new ContainerControlledLifetimeManager());
        }

        /// <summary>
        /// Registers the controller so its views and resources can be resolved.
        /// </summary>
        /// <param name="controller">The controller.</param>
        protected virtual void RegisterController(Type controller)
        {
            var controllerStore = new ControllerStore();
            var configManager = ConfigManager.GetManager();
            using (new ElevatedConfigModeRegion())
            {
                this.RegisterStringResources(controller);
                controllerStore.AddController(controller, configManager);
            }
        }

        /// <summary>
        /// Gets the assembly file names that will be inspected for controllers.
        /// </summary>
        /// <param name="assemblyPath">The assembly path.</param>
        protected virtual IEnumerable<string> GetAssemblyFileNames()
        {
            var controllerAssemblyPath = Path.Combine(HostingEnvironment.ApplicationPhysicalPath, "bin");
            return Directory.EnumerateFiles(controllerAssemblyPath, "*.dll", SearchOption.TopDirectoryOnly);
        }

        /// <summary>
        /// Determines whether the given <paramref name="assemblyFileName"/> is an assembly file that is marked as <see cref="ContainerControllerAttribute"/>.
        /// </summary>
        /// <param name="assemblyFileName">Filename of the assembly.</param>
        /// <returns>True if the given file name is of an assembly file that is marked as <see cref="ContainerControllerAttribute"/>, false otherwise.</returns>
        protected virtual bool IsControllerContainer(string assemblyFileName)
        {
            if (assemblyFileName == null)
                return false;

            bool result;
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += this.CurrentDomain_ReflectionOnlyAssemblyResolve;
            try
            {
                try
                {
                    var reflOnlyAssembly = Assembly.ReflectionOnlyLoadFrom(assemblyFileName);

                    result = reflOnlyAssembly != null &&
                            reflOnlyAssembly.GetCustomAttributesData()
                                .Any(d => d.Constructor.DeclaringType.AssemblyQualifiedName == typeof(ControllerContainerAttribute).AssemblyQualifiedName);
                }
                //We might not be able to load some .DLL files as .NET assemblies. Those files cannot contain controllers.
                catch (IOException)
                {
                    result = false;
                }
                catch (BadImageFormatException)
                {
                    result = false;
                }
            }
            finally
            {
                AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve -= this.CurrentDomain_ReflectionOnlyAssemblyResolve;
            }

            return result;
        }

        /// <summary>
        /// Replaces the current controller factory used by the MVC framework with the factory that is registered by Sitefinity.
        /// </summary>
        protected virtual void ReplaceControllerFactory()
        {
            var factory = ObjectFactory.Resolve<ISitefinityControllerFactory>();
            ControllerBuilder.Current.SetControllerFactory(factory);
        }

        #endregion

        #region Private members

        private Assembly CurrentDomain_ReflectionOnlyAssemblyResolve(object sender, ResolveEventArgs args)
        {
            return Assembly.ReflectionOnlyLoad(args.Name);
        }

        private void RegisterAssemblyPaths(Assembly assembly)
        {
            var assemblyName = assembly.GetName();
            var virtualPath = FrontendManager.VirtualPathBuilder.GetVirtualPath(assembly);

            VirtualPathManager.AddVirtualFileResolver<ResourceResolver>("~/" + virtualPath + "*", assemblyName.Name,
                assemblyName.CodeBase);

            SystemManager.RegisterRoute(assemblyName.Name, new Route(virtualPath + "{*Params}", new RouteHandler<ResourceHttpHandler>()),
                    assemblyName.Name, requireBasicAuthentication: false);
        }

        private void RemoveSitefinityViewEngine()
        {
            var sfViewEngine = ViewEngines.Engines.FirstOrDefault(v => v is SitefinityViewEngine);

            if (sfViewEngine != null)
            {
                ViewEngines.Engines.Remove(sfViewEngine);
            }
        }

        /// <summary>
        /// Registers the controller string resources.
        /// </summary>
        /// <param name="controller">Type of the controller.</param>
        private void RegisterStringResources(Type controller)
        {
            var localizationAttributes = controller.GetCustomAttributes(typeof(LocalizationAttribute), true);
            foreach (var attribute in localizationAttributes)
            {
                var localAttr = (LocalizationAttribute)attribute;
                if (!ObjectFactory.Container.IsRegistered(localAttr.ResourceClass, Res.GetResourceClassId(localAttr.ResourceClass)))
                {
                    Res.RegisterResource(localAttr.ResourceClass);
                }
            }
        }

        #endregion
    }
}
