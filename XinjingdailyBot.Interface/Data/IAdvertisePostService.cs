using XinjingdailyBot.Interface.Data.Base;
using XinjingdailyBot.Model.Models;

namespace XinjingdailyBot.Interface.Data;

/// <summary>
/// 广告消息服务
/// </summary>
public interface IAdvertisePostService : IBaseService<AdvertisePosts>
{
    /// <summary>
    /// 删除旧的广告消息
    /// </summary>
    /// <param name="advertises"></param>
    /// <param name="excludePin"></param>
    /// <returns></returns>
    Task DeleteOldAdPosts(Advertises advertises, bool excludePin);
}
