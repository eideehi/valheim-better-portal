using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterPortal
{
    internal class PortalTagListRow : MonoBehaviour, IPointerDownHandler, IPointerClickHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        public string Tag { get; set; }
        public GameObject Highlight { get; set; }
        public ButtonSfx Sfx { get; set; }
        public Selectable Button { get; set; }
        public Action<PortalTagListRow> Clicked { get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            // Handling the press is what matters: left unhandled it bubbles up to the input field the
            // list hangs under, and TMP_InputField.OnPointerDown moves the caret to the pressed point.
            // The list's frame carries this component too, without a tag, so a press on its padding is
            // swallowed the same way.
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Tag != null && eventData.button == PointerEventData.InputButton.Left)
                Clicked?.Invoke(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            SetHovered(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            SetHovered(false);
        }

        private void SetHovered(bool hovered)
        {
            if (Tag == null) return;

            if (Button)
            {
                // The row's Button is disabled, so it never runs its own hover transition; apply the tint here.
                var colors = Button.colors;
                var tint = hovered ? colors.highlightedColor : colors.normalColor;
                Button.targetGraphic.CrossFadeColor(tint * colors.colorMultiplier, colors.fadeDuration, true, true);
            }
        }
    }
}
