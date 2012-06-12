﻿using System;
using System.IO;
using System.Text;
using System.Web;
using System.Web.UI;
using System.Web.WebPages;
using System.Xml.Linq;
using Composite.AspNet.Razor;
using Composite.Core.Instrumentation;
using Composite.Core.PageTemplates;
using Composite.Core.WebClient.Renderings.Page;

namespace Composite.Plugins.PageTemplates.Razor
{
    internal class RazorPageRenderer : IPageRenderer
    {
        private readonly Core.Collections.Generic.Hashtable<Guid, TemplateRenderingInfo> _renderingInfo;

        public RazorPageRenderer(Core.Collections.Generic.Hashtable<Guid, TemplateRenderingInfo> renderingInfo)
        {
            _renderingInfo = renderingInfo;
        }

        private Page _aspnetPage;
        private PageRenderingJob _job;

        public void AttachToPage(Page renderTaget, PageRenderingJob renderJob)
        {
            _aspnetPage = renderTaget;
            _job = renderJob;

            _aspnetPage.Init += RendererPage;
        }

        private void RendererPage(object sender, EventArgs e)
        {
            Guid templateId = _job.Page.TemplateId;
            var renderingInfo = _renderingInfo[templateId];

            Verify.IsNotNull(renderingInfo, "Missing template '{0}'", templateId);

            var webPage = WebPageBase.CreateInstanceFromVirtualPath(renderingInfo.ControlVirtualPath) as CompositeC1PageTemplate;
            Verify.IsNotNull(webPage, "Razor compilation failed or base type does not inherit '{0}'", typeof(CompositeC1PageTemplate).FullName);

            var functionContextContainer = PageRenderer.GetPageRenderFunctionContextContainer();

            
            using (Profiler.Measure("Evaluating placeholders"))
            {
                TemplateDefinitionHelper.BindPlaceholders(webPage, _job, renderingInfo.Placeholders, functionContextContainer);
            }

            // Executing razor code
            var httpContext = new HttpContextWrapper(HttpContext.Current);
            var startPage = StartPage.GetStartPage(webPage, "_PageStart", new[] { "cshtml" });
            var pageContext = new WebPageContext(httpContext, webPage, startPage);

            var sb = new StringBuilder();
			using (var writer = new StringWriter(sb))
			{
                using(Profiler.Measure("Executing Razor page template"))
                {
                    webPage.ExecutePageHierarchy(pageContext, writer);
                }
			}

            string output = sb.ToString();

            XDocument resultDocument = XDocument.Parse(output);
            
            var controlMapper = (IXElementToControlMapper)functionContextContainer.XEmbedableMapper;
            Control control = PageRenderer.Render(resultDocument, functionContextContainer, controlMapper, _job.Page);

            using (Profiler.Measure("ASP.NET controls: PagePreInit"))
            {
                _aspnetPage.Controls.Add(control);
            }
        }
    }
}
