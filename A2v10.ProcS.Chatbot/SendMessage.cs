﻿using System;
using System.Threading.Tasks;
using A2v10.ProcS.Infrastructure;
using BotCore;
using BotCore.Types.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace A2v10.ProcS.Chatbot
{

	public class StorableOutgoingMessage : IStorable
	{
		public OutgoingMessage Message { get; protected set; }

		public void Restore(IDynamicObject store, IResourceWrapper wrapper)
		{
			var settings = new JsonSerializerSettings();
			settings.Converters.Add(new StringEnumConverter());
			settings.Converters.Add(new InterfaceMapConverter<Keyboard, IKeyboard>());
			settings.Converters.Add(new InterfaceMapConverter<Button, IButton>());
			Message = JsonConvert.DeserializeObject<OutgoingMessage>(store.ToJson(), settings);
		}

		public IDynamicObject Store(IResourceWrapper wrapper)
		{
			var settings = new JsonSerializerSettings();
			settings.Converters.Add(new StringEnumConverter());
			var json = JsonConvert.SerializeObject(Message, settings);
			return DynamicObjectConverters.FromJson(json);
		}

		public void Resolve(IExecuteContext context)
		{
			Message.Text = context.Resolve(Message.Text);
		}
	}


	[ResourceKey(Plugin.Name + ":" + nameof(SendMessageActivity))]
	public class SendMessageActivity : IActivity
	{
		public BotEngine BotEngine { get; set; }
		public String BotKey { get; set; }
		public String ChatId { get; set; }
		public IDynamicObject Message { get; set; }

		public ActivityExecutionResult Execute(IExecuteContext context)
		{
			var m = new StorableOutgoingMessage();
			m.Restore(Message, null);
			m.Resolve(context);
			var mess = new SendMessageMessage(m)
			{
				BotEngine = BotEngine,
				BotKey = BotKey,
				ChatId = Guid.Parse(context.Resolve(ChatId))
			};
			context.SendMessage(mess);
			return ActivityExecutionResult.Complete;
		}
	}

	[ResourceKey(ukey)]
	public class SendMessageMessage : MessageBase<Guid>
	{
		public const string ukey = Plugin.Name + ":" + nameof(SendMessageMessage);
		public BotEngine BotEngine { get; set; }
		public String BotKey { get; set; }
		public Guid ChatId { get; set; }
		public StorableOutgoingMessage Message { get; set; }

		[RestoreWith]
		public SendMessageMessage(Guid correlationId) : base(correlationId)
		{

		}
		public SendMessageMessage(StorableOutgoingMessage message) : base(Guid.NewGuid())
		{
			Message = message;
		}

		public override void Store(IDynamicObject storage, IResourceWrapper wrapper)
		{
			storage.Set("correlationId", CorrelationId.Value);
			storage.Set("botEngine", BotEngine.ToString());
			storage.Set("botKey", BotKey);
			storage.Set("chatId", ChatId);
			storage.Set("message", Message.Store(wrapper));
		}

		public override void Restore(IDynamicObject store, IResourceWrapper wrapper)
		{
			BotEngine = store.Get<BotEngine>("botEngine");
			BotKey = store.Get<String>("botKey");
			ChatId = store.Get<Guid>("chatId");
			var m = new StorableOutgoingMessage();
			m.Restore(store.GetDynamicObject("message"), wrapper);
			Message = m;
		}
	}
}
