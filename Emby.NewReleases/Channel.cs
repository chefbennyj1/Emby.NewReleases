using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Channels;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Channels;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.MediaInfo;
using MediaBrowser.Model.Tasks;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Common.Extensions;

namespace Emby.NewReleases
{
    public class NewReleasesChannel : IChannel, IHasCacheKey, IRequiresMediaInfoCallback
    {
        private ILibraryManager LibraryManager { get; set; }
        private ITaskManager TaskManager { get; set; }       
        private ILogger Log { get; set; }

        
        public NewReleasesChannel(ILibraryManager libraryManager,  ILogManager logManager, ITaskManager taskManager)
        {
            LibraryManager = libraryManager;
            TaskManager = taskManager;
            Log = logManager.GetLogger(Plugin.Instance.Name);

        }
        public string DataVersion => "668";

        public bool IsEnabledFor(string userId)
        {
            return true;
        }

        public InternalChannelFeatures GetChannelFeatures()
        {
            return new InternalChannelFeatures
            {
                ContentTypes = new List<ChannelMediaContentType>
                {
                    ChannelMediaContentType.Movie,

                },

                MediaTypes = new List<ChannelMediaType>
                {
                    ChannelMediaType.Video
                },

                SupportsContentDownloading = true,
                SupportsSortOrderToggle = false,
                MaxPageSize = 9,
                AutoRefreshLevels = 3,
                DefaultSortFields = new List<ChannelItemSortField>()
                {
                    ChannelItemSortField.PremiereDate,
                    ChannelItemSortField.DateCreated
                }

            };
        }

        public async Task<ChannelItemResult> GetChannelItems(InternalChannelItemQuery query, CancellationToken cancellationToken)
        { 
            var items = new List<ChannelItemInfo>();
                      
            
            var libraryResult = LibraryManager.GetItemsResult(new InternalItemsQuery()
            {
                IncludeItemTypes = new[] { "Movie" },
                MinPremiereDate = DateTimeOffset.Now.AddMonths(-8),
                MinDateCreated = DateTimeOffset.Now.AddMonths(-2),
                DtoOptions = new DtoOptions(true)
            });

            foreach (var item in  libraryResult.Items)
            {
                                
                if (item.Path == string.Empty) continue;

                if (items.Exists(i => i.Name == item.Name && i.ProductionYear == item.ProductionYear))
                {
                    var channelItem = items.FirstOrDefault(i => i.Name == item.Name && i.ProductionYear == item.ProductionYear);

                    if(channelItem is null) continue;

                    var mediaSources = item.GetMediaSources(false, false, LibraryManager.GetLibraryOptions(item)); 
                    
                    foreach(var source in mediaSources)
                    {
                        if(channelItem.MediaSources.Exists(s => s.Path == source.Path)) continue;

                        source.Id = $"new_release_{source.Path}".GetMD5().ToString("N");
                        channelItem.MediaSources.Add(source);
                    }

                    continue;
                }

                var sources = item.GetMediaSources(false, false, LibraryManager.GetLibraryOptions(item));
                var sourceList = new List<MediaSourceInfo>();
                foreach(var source in sources)
                {
                    source.Id = $"new_release_{source.Path}".GetMD5().ToString("N");
                    source.Protocol = MediaProtocol.File;
                    if(sourceList.Exists(s => s.Path == source.Path)) continue;
                    sourceList.Add(source);
                }

                items.Add( 
                    new ChannelItemInfo()
                    {                        
                        DateCreated     = item.DateCreated,
                        Name            = item.Name,
                        Id              = $"new_release_{item.InternalId}".GetMD5().ToString("N"),                         
                        RunTimeTicks    = item.RunTimeTicks,                       
                        ProductionYear  = item.ProductionYear,
                        ImageUrl        = item.PrimaryImagePath, 
                        Type            = ChannelItemType.Media,
                        ContentType     = ChannelMediaContentType.Movie,
                        MediaType       = ChannelMediaType.Video,
                        IsLiveStream    = false,
                        OfficialRating  = item.OfficialRating,
                        Overview        = item.Overview,
                        PremiereDate    = item.PremiereDate,
                        Genres          = item.Genres.ToList(),
                        CommunityRating = item.CommunityRating,
                        OriginalTitle   = item.OriginalTitle,
                        ProviderIds     = item.ProviderIds,
                        Studios         = item.Studios.ToList(),
                        People          = LibraryManager.GetItemPeople(item),
                        MediaSources    = sources
                        
                    });             
                

            }
                        

            return await Task.FromResult(new ChannelItemResult
            {
                Items = items,
                TotalRecordCount = items.Count
                
            });

        }

        public async Task<DynamicImageResponse> GetChannelImage(ImageType type, CancellationToken cancellationToken)
        {
            var path = GetType().Namespace + ".Images." + type.ToString().ToLower() + ".jpg";

            return await Task.FromResult(new DynamicImageResponse
            {
                Format = ImageFormat.Jpg,
                IsFallbackImage = true,
                Protocol = MediaProtocol.File,
                Stream = GetType().Assembly.GetManifestResourceStream(path)
            });            
        }

        public IEnumerable<ImageType> GetSupportedChannelImages()
        {
            return new List<ImageType>() { ImageType.Primary };
        }

        public string Name => Plugin.Instance.Name;
        public string Description => Plugin.Instance.Description;
        public ChannelParentalRating ParentalRating => ChannelParentalRating.GeneralAudience; 
        
        public string GetCacheKey(string userId) => Guid.NewGuid().ToString("N");

        public async Task<IEnumerable<MediaSourceInfo>> GetChannelItemMediaInfo(string id, CancellationToken cancellationToken)
        {          

            var channel = await GetChannelItems(new InternalChannelItemQuery(), cancellationToken);

            var item = channel.Items.FirstOrDefault(i => i.Id == id);
            
            if(item.MediaSources.Count <= 1)
            {
                return item.MediaSources.GetRange(0, 0);
            }

            Log.Info($"Sending Media Source info for {item.Name}");
            
            return item.MediaSources.GetRange(1, item.MediaSources.Count -1);            

        }

        

    }

    public static class Extensions
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            HashSet<TKey> seenKeys = new HashSet<TKey>();
            foreach (TSource element in source)
            {
                if (seenKeys.Add(keySelector(element)))
                {
                    yield return element;
                }
            }
        }
    }
}
