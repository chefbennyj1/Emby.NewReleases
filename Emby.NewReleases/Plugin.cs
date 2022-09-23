using Emby.NewReleases.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using System;
using System.IO;

namespace Emby.NewReleases
{
    public class Plugin : BasePlugin<PluginConfiguration>
    {
        public static Plugin Instance {get; set;}
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public override string Name => "New Releases";
        public override string Description => "Spotlight new releases from the media library.";

        public override Guid Id =>  new Guid("372F9460-127A-488D-9F2F-A50AD7A31F1B");
        
        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".thumb.jpg");
        }

    }
}
