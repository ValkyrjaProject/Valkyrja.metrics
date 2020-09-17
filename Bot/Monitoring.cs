using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Valkyrja.coreLite;
using Valkyrja.entities;
using Discord;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;
using Prometheus;
using guid = System.UInt64;

namespace Valkyrja.metrics
{
	public class Monitoring: IModule, IDisposable
	{
		private ValkyrjaClient<Config> Client;

		public Func<Exception, string, guid, Task> HandleException{ get; set; }
		public bool DoUpdate{ get; set; } = true;

		private MetricHandler Prometheus;

		public readonly Gauge TotalMembers = Metrics.CreateGauge("discord_stats_totalmembers", "Discord stats: Total Members");
		public readonly Counter MessagesReceived = Metrics.CreateCounter("discord_stats_messagesreceived", "Discord stats: Messages Received");
		public readonly Counter HelpCounter = Metrics.CreateCounter("discord_stats_helpcounter", "Discord stats: Amount of help questions");
		public readonly Counter UniqueHelpCounter = Metrics.CreateCounter("discord_stats_uniquehelpcounter", "Discord stats: Amount of helped users");

		private readonly List<guid> HelpedUserIds = new List<guid>();

		public List<Command> Init(IValkyrjaClient iClient)
		{
			this.Client = iClient as ValkyrjaClient<Config>;

			if( this.Prometheus == null )
			{
				this.Prometheus = new MetricPusher(this.Client.CoreConfig.PrometheusEndpoint, this.Client.CoreConfig.PrometheusJob, this.Client.CoreConfig.PrometheusInstance, this.Client.CoreConfig.PrometheusInterval);
			}

			this.Prometheus.Start();

			List<Command> commands = new List<Command>();
			this.Client.Events.MessageReceived += OnMessageReceived;
// !helped
			Command newCommand = new Command("helped");
			newCommand.Type = CommandType.Standard;
			newCommand.Description = "This is an example command.";
			newCommand.ManPage = new ManPage("<UserId>", "`<UserId>` - A user(s) who received help.");
			newCommand.RequiredPermissions = PermissionType.Everyone;
			newCommand.OnExecute += async e => {
				List<guid> mentionedUserIds = this.Client.GetMentionedUserIds(e, false);
				foreach( guid userId in mentionedUserIds )
				{
					this.HelpCounter.Inc();

					if( this.HelpedUserIds.Contains(userId) )
						continue;
					this.UniqueHelpCounter.Inc();
				}
				await e.Message.AddReactionAsync(new Emoji("✅"));
			};
			commands.Add(newCommand);

			return commands;
		}

		private Task OnMessageReceived(SocketMessage message)
		{
			if( message.Author.Id == this.Client.DiscordClient.CurrentUser.Id )
				return Task.CompletedTask;
			this.MessagesReceived.Inc();
			return Task.CompletedTask;
		}

		public Task Update(IValkyrjaClient iClient)
		{
			if( DateTime.UtcNow.Hour == 0 && DateTime.UtcNow.Minute >= 0 && DateTime.UtcNow.Minute < 3 )
				this.HelpedUserIds.Clear();

			SocketGuild guild = this.Client.DiscordClient.GetGuild(this.Client.Config.ServerId);
			if( guild != null )
				this.TotalMembers.Set(guild.MemberCount);

			return Task.CompletedTask;
		}

		public void Dispose()
		{
			this.Prometheus.Stop();
			((IDisposable)this.Prometheus)?.Dispose();
		}
	}
}
