using System.Net;
using XinjingdailyBot.Data.WebResponses;
using XinjingdailyBot.Infrastructure.Model;

namespace XinjingdailyBot.Interface.Helper;

/// <summary>
/// Http请求服务
/// </summary>
public interface IHttpHelperService
{
    /// <summary>
    /// 创建Http客户端
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    HttpClient CreateClient(string name);
    /// <summary>
    /// 下载发行版
    /// </summary>
    /// <param name="downloadUrl"></param>
    /// <returns></returns>
    Task<Stream?> DownloadRelease(string? downloadUrl);
    /// <summary>
    /// 获取IP信息
    /// </summary>
    /// <param name="ip"></param>
    /// <returns></returns>
    Task<IpInfoResponse?> GetIpInformation(IPAddress ip);
    /// <summary>
    /// 获取最新的发行版
    /// </summary>
    /// <returns></returns>
    Task<GitHubReleaseResponse?> GetLatestRelease();
    /// <summary>
    /// 下载Telegram文件头
    /// </summary>
    /// <param name="tgFile"></param>
    /// <param name="length"></param>
    /// <returns></returns>
    Task<Stream?> GetTelegramFileHeader(Telegram.Bot.Types.File tgFile, int length);

    /// <summary>
    /// 发送统计信息
    /// </summary>
    /// <returns></returns>
    Task SendStatistic();
}
