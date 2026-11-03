using System;
using System.Collections.Generic;
using System.Reflection;
using Playnite.SDK;
using Playnite.SDK.Plugins;

[assembly: AssemblyTitle("Duplicate Review")]
[assembly: AssemblyDescription("Manual review of exact and suggested title matches in Playnite. No background activity or network access.")]
[assembly: AssemblyCompany("HyunCafe")]
[assembly: AssemblyProduct("Duplicate Review")]
[assembly: AssemblyVersion("0.1.2.0")]
[assembly: AssemblyFileVersion("0.1.2.0")]

namespace DuplicateReview
{
    public sealed class DuplicateReviewPlugin : GenericPlugin
    {
        private static readonly Guid PluginGuid = new Guid("A76D1921-4ECB-5FC7-97AB-9079DF41AE73");
        public override Guid Id { get { return PluginGuid; } }

        public DuplicateReviewPlugin(IPlayniteAPI api) : base(api) { }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem
            {
                Description = "Review duplicate game titles...",
                MenuSection = "@Duplicate Review",
                Action = args2 =>
                {
                    try { new ReviewWindow(PlayniteApi).Show(); }
                    catch (Exception e) { PlayniteApi.Dialogs.ShowErrorMessage("Duplicate Review could not open: " + e.Message, "Duplicate Review"); }
                }
            };
        }
    }
}
