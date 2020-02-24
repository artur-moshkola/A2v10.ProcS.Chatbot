﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using A2v10.ProcS.Infrastructure;
using BotCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace A2v10.ProcS.Chatbot
{
	[ResourceKey(ukey)]
	public class IncomeMessage : MessageBase<Guid>
	{
		public const string ukey = Plugin.Name + ":" + nameof(IncomeMessage);
		public BotEngine BotEngine { get; set; }
		public String BotKey { get; set; }
		public IIncomingMessage Message { get; set; }

		[RestoreWith]
		public IncomeMessage(Guid chatId) : base(chatId)
		{
		}

		public override void Store(IDynamicObject storage)
		{
			storage.Set("botEngine", BotEngine.ToString());
			storage.Set("botKey", BotKey);
			storage.Set("message", DynamicObjectConverters.From(Message));
		}

		public override void Restore(IDynamicObject storage)
		{
			if (!Enum.TryParse<BotEngine>(storage.Get<String>("botEngine"), out var botEngine)) throw new Exception("Cannot restore IncomeMessage. Wrong BotEngine.");
			BotEngine = botEngine;
			BotKey = storage.Get<String>("botKey");
			Message = storage.GetDynamicObject("message").To<RestoredIncomingMessage>();
		}
	}

	internal class RestoredIncomingMessage : IIncomingMessage
	{
		public String Text { get; set; }
		public BotCore.Types.Enums.MessageInType Type { get; set; }
		public User User { get; set; }
		public Location Location { get; set; }
	}

	internal interface IWaitMessageSagaStored
	{
		Boolean IsWaiting { get; set; }
		Guid BookmarkId { get; set; }
		BotEngine BotEngine { get; set; }
		String BotKey { get; set; }
	}

	public class WaitMessageSaga : SagaBaseDispatched<Guid, WaitMessageMessage, IncomeMessage>, IWaitMessageSagaStored
	{
		public const string ukey = Plugin.Name + ":" + nameof(WaitMessageSaga);
		
		private BotManager botManager;
		
		public Boolean IsWaiting { get; set; }
		public Guid BookmarkId { get; set; }
		public BotEngine BotEngine { get; set; }
		public String BotKey { get; set; }

		internal WaitMessageSaga(BotManager botManager) : base(ukey)
		{
			IsWaiting = false;
			this.botManager = botManager;
		}

		protected override Task Handle(IHandleContext context, WaitMessageMessage message)
		{
			BookmarkId = message.BookmarkId;
			BotEngine = message.BotEngine;
			BotKey = message.BotKey;
			CorrelationId.Value = message.CorrelationId.Value;
			IsWaiting = true;
			return Task.CompletedTask;
		}

		protected override Task Handle(IHandleContext context, IncomeMessage message)
		{
			if (IsWaiting)
			{
				context.ResumeBookmark(BookmarkId, DynamicObjectConverters.From(message));
				IsComplete = true;
			}
			else
			{
				var m = new InitBotChatMessage(message.BotEngine, message.BotKey);
				m.ChatId = message.CorrelationId.Value;
				m.Message = message.Message;
				context.SendMessage(m);
				IsComplete = true;
			}
			return Task.CompletedTask;
		}
	}

	internal class WaitMessageSagaFactory : ISagaFactory
	{
		private BotManager botManager;
		
		public WaitMessageSagaFactory(BotManager botManager)
		{
			this.botManager = botManager;
		}

		public string SagaKind => WaitMessageSaga.ukey;

		public ISaga CreateSaga()
		{
			return new WaitMessageSaga(botManager);
		}
	}

	public class WaitMessageSagaRegistrar : ISagaRegistrar
	{
		private Plugin plugin;

		public WaitMessageSagaRegistrar(Plugin plugin)
		{
			this.plugin = plugin;
		}

		public void Register(IResourceManager rmgr, ISagaManager smgr)
		{
			var factory = new WaitMessageSagaFactory(plugin.BotManager);
			rmgr.RegisterResourceFactory(factory.SagaKind, new SagaResourceFactory(factory));
			rmgr.RegisterResources(WaitMessageSaga.GetHandledTypes());
			smgr.RegisterSagaFactory(factory, WaitMessageSaga.GetHandledTypes());
		}
	}
}
