using Microsoft.Extensions.Logging;
using System.Text;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using XinjingdailyBot.Infrastructure.Attribute;
using XinjingdailyBot.Interface.Bot.Common;
using XinjingdailyBot.Interface.Data;
using XinjingdailyBot.Interface.Helper;

namespace XinjingdailyBot.Tasks;

/// <summary>
/// 定期发布审核状态通知
/// </summary>
[Schedule("0 * * * * ?")]
public sealed class ReviewStatusTask(
    ILogger<ReviewStatusTask> _logger,
    IPostService _postService,
    ITelegramBotClient _botClient,
    IChannelService _channelService,
    IReviewStatusService _reviewStatusService,
    IMarkupHelperService _markupHelperService) : IJob
{
    private bool Disabled { get; set; }

    /// <inheritdoc/>
    public async Task Execute(IJobExecutionContext context)
    {
        if (Disabled)
        {
            return;
        }

        _logger.LogInformation("开始定时任务, 更新投稿状态显示");

        var now = DateTime.Now;
        var today = now.AddHours(-now.Hour).AddMinutes(-now.Minute).AddSeconds(-now.Second);

        var todayPost = await _postService.CountAllPosts(today).ConfigureAwait(false);
        var todayAcceptPost = await _postService.CountAcceptedPosts(today).ConfigureAwait(false);
        var todayRejectPost = await _postService.CountRejectedPosts(today).ConfigureAwait(false);
        var todayPaddingPost = await _postService.CountReviewingPosts(today).ConfigureAwait(false);

        if (_channelService.HasSecondChannel)
        {
            var todayAcceptSecondPost = await _postService.CountAcceptedSecondPosts(today).ConfigureAwait(false);
            todayAcceptPost += todayAcceptSecondPost;
        }

        var acceptRate = todayPost > 0 ? (100 * todayAcceptPost / todayPost).ToString("f2") : "--";
        var reviewRate = todayPost > 0 ? (100 * (todayPost - todayPaddingPost) / todayPost).ToString("f2") : "--";

        var sb = new StringBuilder();
        sb.AppendLine($"接受 <code>{todayAcceptPost}</code> 拒绝 <code>{todayRejectPost}</code> 待审核 <code>{todayPaddingPost}</code>");
        sb.AppendLine($"通过率: <code>{acceptRate}%</code> 审核率: <code>{reviewRate}%</code>");
        sb.AppendLine($"#审核统计 [更新于 {now:HH:mm:ss}]");

        Message? statusMsg = null;

        var oldPost = await _reviewStatusService.GetOldReviewStatu().ConfigureAwait(false);

        var reviewGroup = _channelService.ReviewGroup;

        var newestPost = await _postService.GetLatestReviewingPostLink().ConfigureAwait(false);
        var kbd = _markupHelperService.ReviewStatusButton(newestPost);

        if (oldPost != null)
        {
            if (oldPost.CreateAt.Day != now.Day) //隔天的统计
            {
                var oldTime = oldPost.CreateAt;
                var startTime = oldTime.AddHours(-oldTime.Hour).AddMinutes(-oldTime.Minute).AddSeconds(oldTime.Second);
                var endTime = startTime.AddDays(1);

                var post = await _postService.CountAllPosts(startTime, endTime).ConfigureAwait(false);
                var acceptPost = await _postService.CountAcceptedPosts(startTime, endTime).ConfigureAwait(false);
                var rejectPost = await _postService.CountRejectedPosts(startTime, endTime).ConfigureAwait(false);
                var paddingPost = await _postService.CountReviewingPosts(startTime, endTime).ConfigureAwait(false);

                if (_channelService.HasSecondChannel)
                {
                    var acceptSecondPost = await _postService.CountAcceptedSecondPosts(startTime, endTime).ConfigureAwait(false);
                    acceptPost += acceptSecondPost;
                }

                var accept = post > 0 ? (100 * acceptPost / post).ToString("f2") : "--";
                var review = post > 0 ? (100 * (post - paddingPost) / post).ToString("f2") : "--";

                var old = new StringBuilder();
                old.AppendLine($"接受 <code>{acceptPost}</code> 拒绝 <code>{rejectPost}</code> 待审核 <code>{paddingPost}</code>");
                old.AppendLine($"通过率: <code>{accept}%</code> 审核率: <code>{review}%</code>");
                old.AppendLine($"#审核统计 [{oldTime:yyyy-MM-dd}]");

                try
                {
                    try
                    {
                        var oldMsg = await _botClient.EditMessageTextAsync(reviewGroup, (int)oldPost.MessageID, old.ToString(), parseMode: ParseMode.Html).ConfigureAwait(false);
                    }
                    finally
                    {
                        await _botClient.UnpinChatMessageAsync(reviewGroup, (int)oldPost.MessageID).ConfigureAwait(false);
                    }
                }
                finally
                {
                    await _reviewStatusService.DeleteReviewStatus(oldPost).ConfigureAwait(false);
                }
            }
            else // 同一天的统计
            {
                try
                {
                    statusMsg = await _botClient.EditMessageTextAsync(reviewGroup, (int)oldPost.MessageID, sb.ToString(), parseMode: ParseMode.Html, replyMarkup: kbd).ConfigureAwait(false);
                }
                catch (ApiRequestException ex)
                {
                    if (ex.Message.StartsWith("Bad Request: message is not modified"))
                    {
                        return;
                    }
                    // 删除旧的消息
                    await _reviewStatusService.DeleteOldReviewStatus().ConfigureAwait(false);
                    _logger.LogError(ex, "编辑消息失败");
                }
            }
        }

        if (statusMsg == null)
        {
            statusMsg = await _botClient.SendTextMessageAsync(reviewGroup, sb.ToString(), parseMode: ParseMode.Html, replyMarkup: kbd).ConfigureAwait(false);
            try
            {
                await _botClient.PinChatMessageAsync(reviewGroup, statusMsg.MessageId).ConfigureAwait(false);
            }
            catch (ApiRequestException ex)
            {
                if (ex.Message.StartsWith("Bad Request: not enough rights to manage pinned messages in the chat"))
                {
                    Disabled = true;
                    _logger.LogWarning("没有足够的权限管理频道置顶消息");
                    statusMsg = await _botClient.SendTextMessageAsync(reviewGroup, "审核状态显示功能需要机器人具有审核群的置顶消息权限, 为避免消息刷屏已禁用该功能, 请赋予机器人权限后重新启动机器人", parseMode: ParseMode.Html, replyMarkup: kbd).ConfigureAwait(false);
                }
            }

            await _reviewStatusService.CreateNewReviewStatus(statusMsg).ConfigureAwait(false);
        }
    }
}
