using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using Telerik.Sitefinity.Abstractions;
using Telerik.Sitefinity.Configuration;
using Telerik.Sitefinity.Data;
using Telerik.Sitefinity.DynamicModules.Builder;
using Telerik.Sitefinity.DynamicModules.Builder.Install;
using Telerik.Sitefinity.DynamicModules.Builder.Model;
using Telerik.Sitefinity.DynamicModules.Builder.Web.UI;
using Telerik.Sitefinity.Frontend.Mvc.Controllers;
using Telerik.Sitefinity.Frontend.Resources;
using Telerik.Sitefinity.Modules.Pages;
using Telerik.Sitefinity.Modules.Pages.Configuration;
using Telerik.Sitefinity.Pages.Model;

namespace Telerik.Sitefinity.Frontend.DynamicModule
{
    internal class MvcWidgetInstallationStrategy : IWidgetInstallationStrategy
    {
        #region Constructor

        public MvcWidgetInstallationStrategy()
        {
            this.ActionProcessor = new Dictionary<string, Action<WidgetInstallationContext>>
            {
                { "Install", (WidgetInstallationContext context) => this.Install(context) },
                { "Uninstall", (WidgetInstallationContext context) => this.Uninstall(context) },
                { "UnregisterTemplates", (WidgetInstallationContext context) => this.UnregisterTemplates(context) },
                { "UpdateTemplates", (WidgetInstallationContext context) => this.Update(context) },
                { "ValidateToolboxItemTemplateKeys", (WidgetInstallationContext context) => this.ValidateToolboxItemTemplateKeys(context) },
                { "UpdateToolboxItem", (WidgetInstallationContext context) => this.RegisterToolboxItem(context) }
            };
        }

        #endregion

        #region Public Properties

        public Dictionary<string, Action<WidgetInstallationContext>> ActionProcessor
        {
            get;
            private set;
        }

        #endregion

        #region Methods

        public void Process(WidgetInstallationContext context)
        {
            this.pageManager = context.PageManager;
            
            if (this.pageManager == null)
                this.pageManager = PageManager.GetManager();

            Action<WidgetInstallationContext> action;

            if (this.ActionProcessor.TryGetValue(context.ActionName, out action))
                action(context);
        }

        public virtual void Install(WidgetInstallationContext context)
        {
            this.RegisterTemplates(context.DynamicModule, context.DynamicModuleType);
            this.RegisterToolboxItem(context.DynamicModule, context.DynamicModuleType);
        }

        /// <inheritdoc />
        public virtual void Update(WidgetInstallationContext context)
        {
            this.Install(context);
        }

        public virtual void Uninstall(WidgetInstallationContext context)
        {
            this.UnregisterToolboxItem(context.ContentTypeName);
        }

        public virtual void UnregisterTemplates(WidgetInstallationContext context)
        {
            this.UnregisterTemplates(context.ContentTypeName);
        }

        public virtual void RegisterToolboxItem(WidgetInstallationContext context)
        {
            this.RegisterToolboxItem(context.DynamicModule, context.DynamicModuleType);
        }

        public virtual void ValidateToolboxItemTemplateKeys(WidgetInstallationContext context)
        {
            var defaultMasterTemplateId = context.DefaultMasterTemplateId;
            var defaultDetailTemplateId = context.DefaultDetailTemplateId;

            if (defaultMasterTemplateId == Guid.Empty && defaultDetailTemplateId == Guid.Empty)
            {
                if (defaultMasterTemplateId == Guid.Empty && defaultDetailTemplateId == Guid.Empty)
                {
                    this.RegisterTemplates(context.DynamicModule, context.DynamicModuleType);
                }
            }

            this.RegisterToolboxItem(context);
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "dynamicModule")]
        private void RegisterTemplates(Telerik.Sitefinity.DynamicModules.Builder.Model.DynamicModule dynamicModule, DynamicModuleType dynamicModuleType)
        {
            var virtualPathBuilder = new VirtualPathBuilder();
            var area = "~/" + virtualPathBuilder.GetVirtualPath(this.GetType().Assembly);
            var resourceName = area + "Mvc/Views/{0}/Index.cshtml".Arrange(PluralsResolver.Instance.ToPlural(dynamicModuleType.TypeName));
            var dynamicTypeName = dynamicModuleType.GetFullTypeName();
            var content = "<h1>Dynamic module template for <strong>{0}</strong>, installed in the database.</h1>".Arrange(dynamicTypeName);

            this.RegisterTemplate(area, resourceName, ".cshtml", content, dynamicTypeName + "_MVC");
        }

        private void RegisterTemplate(string areaName, string resourceName, string resourceType, string content, string condition)
        {
            var template = this.pageManager.CreatePresentationItem<ControlPresentation>();
            template.AreaName = areaName;
            template.NameForDevelopers = resourceName;
            template.DataType = resourceType;
            template.Data = content;
            template.Condition = condition;
            this.pageManager.SaveChanges();
        }

        private void RegisterToolboxItem(Telerik.Sitefinity.DynamicModules.Builder.Model.DynamicModule dynamicModule, DynamicModuleType moduleType)
        {
            this.UnregisterToolboxItem(moduleType.GetFullTypeName());

            var configurationManager = ConfigManager.GetManager();
            var toolboxesConfig = configurationManager.GetSection<ToolboxesConfig>();
            var section = this.GetToolboxSection(toolboxesConfig);
            if (section == null)
                return;

            if (moduleType.ParentModuleTypeId == Guid.Empty)
            {
                var toolboxItem = new ToolboxItem(section.Tools)
                {
                    Name = moduleType.GetFullTypeName() + "_MVC",
                    Title = PluralsResolver.Instance.ToPlural(moduleType.DisplayName) + " MVC",
                    Description = string.Empty,
                    ModuleName = dynamicModule.Name,
                    CssClass = "sfNewsViewIcn",
                    ControlType = typeof(Telerik.Sitefinity.Mvc.Proxy.MvcControllerProxy).AssemblyQualifiedName,
                    ControllerType = typeof(DynamicContentController).FullName,
                    Parameters = new NameValueCollection() 
                    { 
                        { "ControllerName", typeof(DynamicContentController).FullName },
                        { "ContentTypeName", moduleType.GetFullTypeName() }
                    }
                };

                section.Tools.Add(toolboxItem);

                configurationManager.SaveSection(toolboxesConfig);
            }
        }

        private ToolboxSection GetToolboxSection(ToolboxesConfig toolboxesConfig)
        {
            var pageControls = toolboxesConfig.Toolboxes["PageControls"];
            var section = pageControls
                .Sections
                .Where<ToolboxSection>(e => e.Name == ToolboxesConfig.ContentToolboxSectionName)
                .FirstOrDefault();

            return section;
        }

        private string GetToolboxItemName(string contentTypeName)
        {
            return contentTypeName + "_MVC";
        }

        private void UnregisterToolboxItem(string contentTypeName)
        {
            var configurationManager = ConfigManager.GetManager();
            var toolboxesConfig = configurationManager.GetSection<ToolboxesConfig>();
            var section = this.GetToolboxSection(toolboxesConfig);
            if (section == null)
                return;

            var toolboxItemName = this.GetToolboxItemName(contentTypeName);
            var toolboxItem = section.Tools.FirstOrDefault<ToolboxItem>(e => e.Name == toolboxItemName);
            if (toolboxItem != null)
            {
                section.Tools.Remove(toolboxItem);
                configurationManager.SaveSection(toolboxesConfig);
            }
        }

        private void UnregisterTemplates(string contentTypeName)
        {
            var templatesToDelete = this.pageManager.GetPresentationItems<ControlPresentation>()
                .Where(c => c.Condition == contentTypeName + "_MVC")
                .ToArray();

            foreach (var template in templatesToDelete)
            {
                this.pageManager.Delete(template);
            }

            this.pageManager.SaveChanges();
        }

        #endregion

        #region Private fields and constants

        private PageManager pageManager;

        #endregion
    }
}
