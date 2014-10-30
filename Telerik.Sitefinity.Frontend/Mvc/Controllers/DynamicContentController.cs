using System;
using System.Web.Mvc;
using Telerik.Sitefinity.Abstractions;
using Telerik.Sitefinity.Frontend.Mvc.Models;

namespace Telerik.Sitefinity.Frontend.Mvc.Controllers
{
    /// <summary>
    /// Controller for dynamic modules.
    /// </summary>
    public class DynamicContentController : Controller
    {
        /// <summary>
        /// The default Index action of the controller.
        /// </summary>
        /// <param name="contentTypeName">Name of the dynamic content type.</param>
        public virtual ActionResult Index(string contentTypeName)
        {
            return this.View("Index");
        }
    }
}
