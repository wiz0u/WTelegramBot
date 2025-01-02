// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

global using System.ComponentModel;
global using System.Diagnostics.CodeAnalysis;
global using System.Text.Json;
global using System.Text.Json.Serialization;
global using Telegram.Bot.Extensions;
global using Telegram.Bot.Requests.Abstractions;
global using Telegram.Bot.Serialization;
global using Telegram.Bot.Types;
global using Telegram.Bot.Types.Enums;
global using Telegram.Bot.Types.InlineQueryResults;
global using Telegram.Bot.Types.Passport;
global using Telegram.Bot.Types.Payments;
global using Telegram.Bot.Types.ReplyMarkups;
global using BotCommand = Telegram.Bot.Types.BotCommand;
global using BotCommandScope = Telegram.Bot.Types.BotCommandScope;
global using ForumTopic = Telegram.Bot.Types.ForumTopic;
global using InputFile = Telegram.Bot.Types.InputFile;
global using InputMedia = Telegram.Bot.Types.InputMedia;
global using LabeledPrice = Telegram.Bot.Types.Payments.LabeledPrice;
global using ShippingOption = Telegram.Bot.Types.Payments.ShippingOption;

[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~M:WTelegram.Bot.GetFile(System.String)~System.Threading.Tasks.Task{Telegram.Bot.Types.TGFile}")]
[assembly: SuppressMessage("Performance", "CA1822:Mark members as static", Scope = "member", Target = "~P:Telegram.Bot.WTelegramBotClient.LocalBotServer")]
[assembly: SuppressMessage("Performance", "CA1866:Use char overload")]

#if NETSTANDARD2_0
#pragma warning disable CS9113
namespace System.Runtime.CompilerServices
{
	internal static class RuntimeHelpers
	{
		public static T[] GetSubArray<T>(T[] array, Range range)
		{
			if (array == null) throw new ArgumentNullException();
			var (offset, length) = range.GetOffsetAndLength(array.Length);
			if (length == 0) return [];
			var dest = typeof(T).IsValueType || typeof(T[]) == array.GetType() ? new T[length]
				: (T[])Array.CreateInstance(array.GetType().GetElementType()!, length);
			Array.Copy(array, offset, dest, 0, length);
			return dest;
		}
	}
	[EditorBrowsable(EditorBrowsableState.Never)]
	internal class IsExternalInit { }
	[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = false, Inherited = false)]
	internal sealed class CallerArgumentExpressionAttribute(string parameterName) : Attribute { }
}
#endif

// These stubs are just to make the compiler happy with existing Telegram.Bot code

namespace JetBrains.Annotations
{
	[AttributeUsage(AttributeTargets.Class|AttributeTargets.Interface)]
	class PublicAPIAttribute : Attribute;
}
