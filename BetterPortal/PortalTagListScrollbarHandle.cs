using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BetterPortal
{
    // Pressing the Scrollbar itself would deselect the input field, since an enabled Scrollbar is an
    // ISelectHandler. The list puts this transparent overlay over the bar and forwards the pointer.
    internal class PortalTagListScrollbarHandle : MonoBehaviour, IPointerDownHandler,
        IPointerUpHandler, IInitializePotentialDragHandler, IBeginDragHandler, IDragHandler
    {
        public Scrollbar Target { get; set; }

        public void OnPointerDown(PointerEventData eventData)
        {
            Target.OnPointerDown(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Target.OnPointerUp(eventData);
        }

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            Target.OnInitializePotentialDrag(eventData);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Target.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Target.OnDrag(eventData);
        }
    }
}
