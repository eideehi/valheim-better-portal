using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace BetterPortal
{
    internal static class PortalTagList
    {
        private const int MaxVisibleRows = 6;
        private const float Padding = 6f;
        private const float Gap = 4f;
        private const float ScreenMargin = 8f;
        private const int SortingOrder = 30000;

        private static readonly List<PortalTagListRow> Rows = new List<PortalTagListRow>();

        private static TextInput _owner;
        private static TextInput _failedOwner;
        private static RectTransform _root;
        private static RectTransform _viewport;
        private static RectTransform _content;
        private static RectTransform _scrollbarOverlay;
        private static GameObject _rowPrefab;
        private static float _pitch;
        private static float _scrollbarWidth;
        private static bool _shown;
        private static string _lastText;
        private static int _visibleRows;

        public static bool IsShown => _shown && _root && _root.gameObject.activeInHierarchy;

        public static void Update(TextInput input, TeleportWorldExtension receiver)
        {
            if (!EnsureBuilt(input)) return;
            if (ZInput.IsGamepadActive())
            {
                Hide();
                return;
            }

            if (!_shown) Show(input, receiver);
            if (!_shown) return;

            var text = input.m_inputField.text;
            if (text == _lastText) return;

            _lastText = text;
            RefreshHighlight();
            ScrollTo(text);
        }

        public static void Hide()
        {
            if (!_shown) return;

            if (_root && _root.gameObject.activeSelf)
                _root.gameObject.SetActive(false);
            _shown = false;
            _lastText = null;
        }

        private static bool EnsureBuilt(TextInput input)
        {
            if (_root && _owner == input) return true;
            if (_failedOwner == input) return false;
            if (!InventoryGui.instance) return false;

            var dialog = InventoryGui.instance.m_textsDialog;
            var prefab = dialog ? dialog.m_elementPrefab : null;
            if (!IsUsableRowPrefab(prefab) || !dialog.m_leftScrollbar || !input.m_inputField.image)
            {
                BetterPortal.Logger.Error(
                    "Destination tag list disabled: the vanilla UI parts it is built from are missing " +
                    "or do not match what this build of BetterPortal expects. Typing the destination " +
                    "tag and the Insert/Up/Down keys keep working.");
                _failedOwner = input;
                return false;
            }

            _rowPrefab = prefab;
            _pitch = dialog.m_spacing;
            _owner = input;
            Build(input);
            return true;
        }

        private static bool IsUsableRowPrefab(GameObject prefab)
        {
            if (!prefab || !(prefab.transform is RectTransform)) return false;

            var label = Utils.FindChild(prefab.transform, "name");
            return label && label.GetComponent<TMP_Text>() &&
                   Utils.FindChild(prefab.transform, "selected");
        }

        private static void Build(TextInput input)
        {
            Rows.Clear();
            _shown = false;
            _lastText = null;

            var field = input.m_inputField;
            var layer = field.gameObject.layer;
            var rootCanvas = GetRootCanvas(input);

            // The list hangs under the input field because a press on it must leave the field selected:
            // on pointer-down the UI input module deselects the current object unless the nearest active
            // ISelectHandler above the pressed object is that same object. With the rows' own
            // ISelectHandler behaviours disabled (see CreateRow), the nearest one is the field itself.
            var root = CreateRect("BetterPortal.TagList", field.transform, layer);
            root.SetAsLastSibling();

            // A nested canvas with its own sorting order draws and receives raycasts above the rest of
            // the dialog regardless of sibling order, and is not clipped by a mask on the field or the
            // panel. TMP_Dropdown sets up its popup list the same way.
            var canvas = root.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;
            canvas.sortingLayerID = rootCanvas.sortingLayerID;
            root.gameObject.AddComponent<GraphicRaycaster>();

            var frame = root.gameObject.AddComponent<Image>();
            var fieldImage = field.image;
            frame.sprite = fieldImage.sprite;
            frame.type = fieldImage.type;
            frame.fillCenter = fieldImage.fillCenter;
            frame.pixelsPerUnitMultiplier = fieldImage.pixelsPerUnitMultiplier;
            frame.color = fieldImage.color;
            frame.material = fieldImage.material;

            var scroll = root.gameObject.AddComponent<ScrollRect>();
            root.gameObject.AddComponent<PortalTagListRow>();

            _viewport = CreateRect("Viewport", root, layer);
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = new Vector2(Padding, Padding);
            _viewport.offsetMax = new Vector2(-Padding, -Padding);
            _viewport.gameObject.AddComponent<RectMask2D>();

            _content = CreateRect("Content", _viewport, layer);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero;
            _content.sizeDelta = Vector2.zero;

            scroll.content = _content;
            scroll.viewport = _viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = false;
            // The UI input module's wheel delta depends on its configured units per tick, so ask it for
            // the tick count to scroll exactly one row per tick.
            scroll.scrollSensitivity = _pitch *
                                       EventSystem.current.currentInputModule
                                           .ConvertPointerEventScrollDeltaToTicks(Vector2.up).y;

            var template = InventoryGui.instance.m_textsDialog.m_leftScrollbar;
            _scrollbarWidth = ((RectTransform)template.transform).rect.width;
            var bar = Object.Instantiate(template, root);
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            // The clone carries the compendium list's listeners.
            bar.onValueChanged = new Scrollbar.ScrollEvent();
            // An enabled Scrollbar is an ISelectHandler, so pressing it would deselect the input field.
            // Its graphics stay out of raycasts and the overlay below receives the pointer instead.
            foreach (var graphic in bar.GetComponentsInChildren<Graphic>(true))
                graphic.raycastTarget = false;
            PlaceAtRightEdge((RectTransform)bar.transform);
            scroll.verticalScrollbar = bar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;

            var overlay = CreateRect("ScrollbarOverlay", root, layer);
            PlaceAtRightEdge(overlay);
            overlay.gameObject.AddComponent<Image>().color = Color.clear;
            overlay.gameObject.AddComponent<PortalTagListScrollbarHandle>().Target = bar;
            _scrollbarOverlay = overlay;

            root.gameObject.SetActive(false);
            _root = root;
        }

        private static RectTransform CreateRect(string name, Transform parent, int layer)
        {
            var gameObject = new GameObject(name, typeof(RectTransform)) { layer = layer };
            var rect = (RectTransform)gameObject.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        private static void PlaceAtRightEdge(RectTransform rect)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-Padding, 0f);
            rect.sizeDelta = new Vector2(_scrollbarWidth, -2f * Padding);
        }

        private static Canvas GetRootCanvas(TextInput input)
        {
            return input.m_inputField.GetComponentInParent<Canvas>().rootCanvas;
        }

        private static void Show(TextInput input, TeleportWorldExtension receiver)
        {
            // Every tag but the edited portal's own is listed, none is filtered out by the field text:
            // Up/Down rotation writes a whole tag into the field, which would collapse a filtered list
            // to that single row.
            var ownTag = receiver.GetPortalTag();
            var tags = TextInputExtension.GetPortalTags()
                .Where(tag => !string.IsNullOrEmpty(tag) && tag != ownTag)
                .ToList();
            if (tags.Count == 0)
            {
                Hide();
                return;
            }

            foreach (var row in Rows)
                Object.Destroy(row.gameObject);
            Rows.Clear();

            for (var index = 0; index < tags.Count; index++)
                Rows.Add(CreateRow(tags[index], index));

            var first = (RectTransform)Rows[0].transform;
            var width = Mathf.Approximately(first.anchorMin.x, first.anchorMax.x)
                ? first.rect.width
                : ((RectTransform)input.m_inputField.transform).rect.width;

            _content.sizeDelta = new Vector2(0f, tags.Count * _pitch);
            _content.anchoredPosition = Vector2.zero;
            _root.gameObject.SetActive(true);
            _shown = true;
            _lastText = null;
            Layout(input, width, tags.Count);
        }

        private static PortalTagListRow CreateRow(string tag, int index)
        {
            var gameObject = Object.Instantiate(_rowPrefab, _content);
            // Disabled rather than destroyed: ButtonTextColor and ButtonImageColor on the row read its
            // Button every frame. Disabling covers ButtonSfx as well, which is also an ISelectHandler.
            Selectable tinted = null;
            foreach (var behaviour in gameObject.GetComponentsInChildren<Behaviour>(true))
            {
                if (!(behaviour is ISelectHandler)) continue;

                behaviour.enabled = false;
                // A disabled Selectable never runs its OnEnable state transition, so apply its Normal
                // tint here; otherwise the row graphic keeps the white default.
                var selectable = behaviour as Selectable;
                if (selectable && selectable.transition == Selectable.Transition.ColorTint && selectable.targetGraphic)
                {
                    selectable.targetGraphic.CrossFadeColor(
                        selectable.colors.normalColor * selectable.colors.colorMultiplier, 0f, true, true);
                    tinted = selectable;
                }
            }

            var row = gameObject.AddComponent<PortalTagListRow>();
            row.Tag = tag;
            row.Highlight = Utils.FindChild(gameObject.transform, "selected").gameObject;
            row.Sfx = gameObject.GetComponentInChildren<ButtonSfx>(true);
            row.Button = tinted;
            row.Clicked = OnRowClicked;
            Utils.FindChild(gameObject.transform, "name").GetComponent<TMP_Text>().text =
                CensorShittyWords.FilterUGC(tag, UGCType.Text, 0L);
            gameObject.SetActive(true);
            // The content and scroll math assume each row fills one pitch slot from its top.
            var rect = (RectTransform)gameObject.transform;
            rect.anchorMin = new Vector2(rect.anchorMin.x, 1f);
            rect.anchorMax = new Vector2(rect.anchorMax.x, 1f);
            rect.pivot = new Vector2(rect.pivot.x, 1f);
            rect.anchoredPosition = new Vector2(0f, -index * _pitch);
            return row;
        }

        private static void Layout(TextInput input, float width, int count)
        {
            var field = (RectTransform)input.m_inputField.transform;
            var canvasRect = (RectTransform)GetRootCanvas(input).transform;
            var bottom = FindDialogBottom(input, field, canvasRect.rect.height);

            _root.anchorMin = new Vector2(0.5f, 0f);
            _root.anchorMax = new Vector2(0.5f, 0f);
            _root.pivot = new Vector2(0.5f, 1f);
            _root.anchoredPosition = new Vector2(0f, bottom - field.rect.yMin - Gap);

            var top = canvasRect.InverseTransformPoint(_root.position).y;
            // The canvas and the list can have different scales, while the rows are sized in list units.
            var scale = _root.lossyScale.y / canvasRect.lossyScale.y;
            var available = (top - canvasRect.rect.yMin - ScreenMargin) / scale;
            var fit = Mathf.Max(1, Mathf.FloorToInt((available - 2f * Padding) / _pitch));
            _visibleRows = Mathf.Min(MaxVisibleRows, Mathf.Min(count, fit));
            // The frame widens for the scrollbar rather than narrowing the viewport, which would clip rows
            // that keep the compendium row's fixed width.
            var scrollable = count > _visibleRows;
            var barSpace = scrollable ? _scrollbarWidth + Gap : 0f;
            _viewport.offsetMax = new Vector2(-(Padding + barSpace), -Padding);
            _scrollbarOverlay.gameObject.SetActive(scrollable);
            _root.sizeDelta = new Vector2(width + 2f * Padding + barSpace, _visibleRows * _pitch + 2f * Padding);
        }

        /// <summary>
        /// Returns the lowest point of the dialog in the field's local space, so that the list hangs
        /// below the whole dialog rather than over its buttons. Screen-sized graphics, such as a dimmer
        /// behind the dialog, are skipped because they would push the list off the screen.
        /// </summary>
        private static float FindDialogBottom(TextInput input, RectTransform field, float canvasHeight)
        {
            var corners = new Vector3[4];
            var bottom = field.rect.yMin;
            foreach (var graphic in input.m_panel.GetComponentsInChildren<Graphic>())
            {
                var rect = graphic.rectTransform;
                if (rect.IsChildOf(_root) || rect.rect.height >= 0.6f * canvasHeight) continue;

                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                    bottom = Mathf.Min(bottom, field.InverseTransformPoint(corner).y);
            }

            return bottom;
        }

        private static void RefreshHighlight()
        {
            var text = _owner.m_inputField.text;
            foreach (var row in Rows)
                row.Highlight.SetActive(row.Tag == text);
        }

        private static void ScrollTo(string text)
        {
            var index = Rows.FindIndex(row => row.Tag == text);
            if (index < 0)
                index = Rows.FindIndex(row =>
                    row.Tag.StartsWith(text, StringComparison.OrdinalIgnoreCase));
            if (index < 0) return;

            var y = _content.anchoredPosition.y;
            var viewHeight = _visibleRows * _pitch;
            if (index * _pitch < y)
                y = index * _pitch;
            else if ((index + 1) * _pitch > y + viewHeight)
                y = (index + 1) * _pitch - viewHeight;
            _content.anchoredPosition = new Vector2(0f, y);
        }

        private static void OnRowClicked(PortalTagListRow row)
        {
            TextInputExtension.SetText(_owner, row.Tag);
            _owner.m_inputField.stringPosition = _owner.m_inputField.text.Length;
            // The row's own ButtonSfx is disabled (see CreateRow), so its click sound is played here.
            if (row.Sfx && row.Sfx.m_sfxPrefab)
                Object.Instantiate(row.Sfx.m_sfxPrefab);
        }
    }
}
