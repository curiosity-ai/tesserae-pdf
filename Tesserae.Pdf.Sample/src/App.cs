using System;
using System.Collections.Generic;
using System.Linq;
using Tesserae;
using static Transpose.Core.dom;
using static Tesserae.UI;

namespace Tesserae.Pdf.Sample
{
    /// <summary>
    /// A gallery app exercising each Tesserae.Pdf feature so it can be eyeballed in a browser,
    /// shaped like Tesserae's own sample gallery: a sidebar of pages on the left, one page per
    /// feature on the right, and a route per page so any of them can be linked to or opened in its
    /// own tab.
    ///
    /// Every document these pages open is a small PDF served from the app's own
    /// <c>assets/pdfs/</c>, so a page can be loaded straight into a browser check with nothing else
    /// running. The package fetches PDFs over plain HTTP like any other asset, which means a page
    /// here is also what the wiring looks like from a host app's point of view: swap the URL for one
    /// of your own endpoints and nothing else changes.
    /// </summary>
    internal static class App
    {
        private const string _sidebarOpenStateKey = "tss-pdf-sidebar-open-close";

        private static void Main()
        {
            document.body.style.overflow = "hidden";

            // Ensure the viewport meta tag is present so that mobile browsers use the device width
            // instead of rendering at a desktop width and scaling down.
            if (document.head.querySelector("meta[name='viewport']") is null)
            {
                var viewportMeta = document.createElement("meta");
                viewportMeta["name"]    = "viewport";
                viewportMeta["content"] = "width=device-width, initial-scale=1.0, maximum-scale=5.0";
                document.head.appendChild(viewportMeta);
            }

            // Adds/removes the tss-mobile class on body whenever the viewport is 768px or narrower.
            Theme.EnableMobileDetection(breakpoint: 768);

            var allSidebarItems     = new List<ISidebarItem>();
            var sampleToSidebarItem = new Dictionary<SamplePage, ISidebarItem>();

            void SelectSidebar(ISidebarItem toSelect)
            {
                allSidebarItems.ForEach(i => i.IsSelected = i == toSelect);
            }

            var currentPage = new SettableObservable<SamplePage>(null);

            currentPage.Observe(selected =>
            {
                if (selected is object && sampleToSidebarItem.TryGetValue(selected, out var item))
                {
                    SelectSidebar(item);
                }
            });

            var sidebar = Sidebar();

            // On mobile we use navbar mode: horizontal bar + full-screen sliding drawer. Evaluated
            // once at startup; the CSS (tss-mobile class) handles the visual switch on resize.
            if (Theme.IsMobileMode)
            {
                sidebar.AsNavbar();
            }

            sidebar.AddHeader(new SidebarText("header", "Tesserae.Pdf", "TSS", textSize: TextSize.XLarge, textWeight: TextWeight.Bold));

            var searchBox = new SidebarSearchBox("search", "Search...");
            searchBox.OnSearch(term => sidebar.Search(term));
            sidebar.AddHeader(searchBox);

            sidebar.AddHeader(new SidebarButton("SOURCE_CODE", UIcons.CodeBranch, "Source Code",
                    new SidebarCommand(UIcons.ArrowUpRightFromSquare).Tooltip("Open repository on GitHub")
                       .OnClick(() => window.open("https://github.com/curiosity-ai/tesserae-pdf", "_blank")))
               .CommandsAlwaysVisible()
               .OnClick(() => window.open("https://github.com/curiosity-ai/tesserae-pdf", "_blank")));

            // A page is only built when it is opened, so one visit creates one page's viewers rather
            // than every page's at startup - and leaving a page unmounts them, which exercises the
            // components' teardown on every click.
            var contentArea = Defer(currentPage, async page => page is null
                ? (IComponent)CenteredCardWithBackground(Message("Tesserae.Pdf", "A pdf.js document viewer and page renderer for Tesserae. Pick a feature on the left.").Icon(UIcons.FilePdf))
                : VStack().S().ScrollY().Children((await page.ContentGenerator()).WS().MinHeight(100.percent())));

            // On mobile the sidebar is a fixed top navbar, so the layout is vertical (sidebar then
            // content). On desktop it is horizontal (sidebar left, content right).
            Stack pageContent;

            if (Theme.IsMobileMode)
            {
                pageContent = VStack().Class("tss-page-layout").S().Children(sidebar.WS(), contentArea.WS().H(1).Grow());
            }
            else
            {
                pageContent = HStack().Class("tss-page-layout").S().Children(sidebar.HS(), contentArea.HS().W(1).Grow());
            }

            MountToBody(pageContent);

            // Important: reflection only works here because the metadata is emitted inline with the
            // JavaScript rather than into a separate .meta.js file - i.e. tps.json needs
            //     "reflection": { "disabled": false, "target": "inline" }
            var samples = typeof(ISample).Assembly.GetTypes()
               .Where(t => typeof(ISample).IsAssignableFrom(t) && !t.IsInterface)
               .Select(sampleType =>
                {
                    var details = sampleType.GetCustomAttributes(typeof(SampleDetailsAttribute), true).FirstOrDefault() as SampleDetailsAttribute;
                    var group   = details is object ? details.Group : "Others";
                    var order   = details is object ? details.Order : 0;
                    var icon    = details is object ? details.Icon : UIcons.Circle;

                    return new SamplePage(sampleType.Name, SamplePage.FormatName(sampleType), group, order, icon,
                        async () => await Activator.CreateInstanceAsync(sampleType) as IComponent);
                })
               .ToDictionary(s => s.Name, s => s);

            var openClose = new SidebarCommand(UIcons.AngleLeft).Tooltip("Close Sidebar");

            // In mobile/navbar mode the drawer always starts closed (the hamburger opens it). On
            // desktop, restore the last open/closed preference.
            if (!Theme.IsMobileMode)
            {
                var sidebarOpenState = bool.TryParse(localStorage.getItem(_sidebarOpenStateKey), out var v) ? v : true;
                sidebar.Closed(!sidebarOpenState);
            }

            openClose.OnClick(() =>
            {
                sidebar.Toggle();

                if (sidebar.IsClosed)
                {
                    openClose.SetIcon(UIcons.AngleRight).Tooltip("Open Sidebar");
                    localStorage.setItem(_sidebarOpenStateKey, false.ToString());
                }
                else
                {
                    openClose.SetIcon(UIcons.AngleLeft).Tooltip("Close Sidebar");
                    localStorage.setItem(_sidebarOpenStateKey, true.ToString());
                }
            });

            var lightDark = new SidebarCommand(Theme.IsDark ? UIcons.Moon : UIcons.Sun).Tooltip(Theme.IsDark ? "Dark Mode" : "Light Mode");

            lightDark.OnClick(() =>
            {
                if (Theme.IsDark)
                {
                    Theme.Light();
                    lightDark.SetIcon(UIcons.Sun).Tooltip("Light Mode");
                }
                else
                {
                    Theme.Dark();
                    lightDark.SetIcon(UIcons.Moon).Tooltip("Dark Mode");
                }
            });

            sidebar.AddFooter(new SidebarCommands("CONFIG", lightDark, openClose));

            var groupIndex = 0;

            foreach (var group in samples.Values.GroupBy(s => s.Group).OrderBy(g => g.Key))
            {
                sidebar.AddContent(new SidebarSeparator(group.Key + groupIndex++, group.Key));

                var itemIndex = 0;

                foreach (var item in group.OrderBy(s => s.Order).ThenBy(s => s.Name.ToLower()))
                {
                    var sidebarItem = new SidebarButton(item.Name + itemIndex++, item.Icon, item.Name,
                        new SidebarCommand(UIcons.ArrowUpRightFromSquare).Tooltip("Open in new tab").OnClick(() => window.open($"#/view/{item.Name}", "_blank")));

                    sidebarItem.OnClick(() =>
                    {
                        // Push updates the URL without reloading, so the page has a route of its own
                        // to link to, and the browser's back button walks the pages visited.
                        Router.Push($"#/view/{item.Name}");

                        currentPage.Value = item;
                    });

                    sidebar.AddContent(sidebarItem);
                    allSidebarItems.Add(sidebarItem);
                    sampleToSidebarItem[item] = sidebarItem;
                }
            }

            Router.Register("home", "/", _ => currentPage.Value = null);

            foreach (var kv in samples)
            {
                Router.Register($"#/view/{kv.Key.Replace(" ", "%20")}", _ => currentPage.Value = kv.Value);
            }

            Router.Initialize();

            // Forcibly match the current route at first load: the routes were only just registered,
            // and we want them matched against the current URL without changing it.
            Router.Refresh(onDone: Router.ForceMatchCurrent);
        }
    }
}
