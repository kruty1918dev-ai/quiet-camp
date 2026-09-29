namespace Kruty1918.UIActions.API
{
    /// <summary>
    /// Необов'язковий sink, який отримує результат кожної дії після виконання.
    /// Використовується презентаційними шарами (аудіо-відгук), не змінює маршрутизацію.
    /// </summary>
    public interface IUiActionFeedbackSink
    {
        void OnActionExecuted(in UiActionRequest request, in UiActionResult result);
    }
}
