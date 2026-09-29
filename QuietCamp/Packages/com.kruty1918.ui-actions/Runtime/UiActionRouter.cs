using System;
using System.Collections.Generic;
using Kruty1918.UIActions.API;
using UnityEngine;

namespace Kruty1918.UIActions.Runtime
{
    public sealed class UiActionRouter : IUiActionRouter
    {
        private readonly Dictionary<UiActionId, IUiActionHandler> _handlers = new();
        private readonly IUiActionJournal _journal;
        private readonly IUiActionFeedbackSink _feedbackSink;

        public UiActionRouter(
            List<IUiActionHandler> handlers,
            IUiActionJournal journal,
            IUiActionFeedbackSink feedbackSink = null)
        {
            _journal = journal;
            _feedbackSink = feedbackSink;
            RegisterHandlers(handlers ?? new List<IUiActionHandler>());
        }

        public UiActionResult Execute(
            UiActionId actionId,
            UiActionSource source = UiActionSource.Programmatic,
            string contextId = null,
            string targetId = null)
        {
            return Execute(new UiActionRequest(
                actionId,
                source,
                contextId,
                targetId));
        }

        public UiActionResult Execute(in UiActionRequest request)
        {
            UiActionResult result = ExecuteCore(request);
            _journal?.Record(request, result);
            _feedbackSink?.OnActionExecuted(request, result);
            return result;
        }

        private UiActionResult ExecuteCore(in UiActionRequest request)
        {
            if (!UiActionId.IsValid(request.ActionId.Value))
            {
                return UiActionResult.Rejected(UiActionReason.ActionUnavailable);
            }

            if (!_handlers.TryGetValue(request.ActionId, out IUiActionHandler handler)
                || handler == null)
            {
                return UiActionResult.Rejected(UiActionReason.ActionUnavailable);
            }

            return handler.Execute(request);
        }

        private void RegisterHandlers(IReadOnlyList<IUiActionHandler> handlers)
        {
            for (int handlerIndex = 0; handlerIndex < handlers.Count; handlerIndex++)
            {
                IUiActionHandler handler = handlers[handlerIndex];
                if (handler?.ActionIds == null)
                    continue;

                foreach (UiActionId actionId in handler.ActionIds)
                {
                    if (!UiActionId.IsValid(actionId.Value))
                    {
                        throw new InvalidOperationException(
                            $"UI action handler '{handler.GetType().Name}' declared invalid action id '{actionId}'.");
                    }

                    if (_handlers.TryGetValue(actionId, out IUiActionHandler existing))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate UI action handler registration for '{actionId}': "
                            + $"'{existing.GetType().Name}' and '{handler.GetType().Name}'.");
                    }

                    _handlers.Add(actionId, handler);
                }
            }
        }
    }
}
