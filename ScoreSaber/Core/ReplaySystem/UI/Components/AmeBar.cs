﻿#region

using BeatSaberMarkupLanguage;
using HMUI;
using System.Linq;
using TMPro;
using UnityEngine;

#endregion

namespace ScoreSaber.Core.ReplaySystem.UI.Components {
    internal class AmeBar : MonoBehaviour {
        private static Material _mainUIFontMaterial;
        private CurvedTextMeshPro _currentTimeText;

        private CurvedTextMeshPro _endTimeText;
        private RectTransform _fillBarTransform;

        private RectTransform _otherTransform;
        private RectTransform _rectTransform;

        public float barFill {
            get => _fillBarTransform.anchorMax.x;
            set => _fillBarTransform.anchorMax = new Vector2(Mathf.Lerp(-1f, 1f, value), _fillBarTransform.anchorMax.y);
        }

        public float currentTime {
            set => _currentTimeText.text = string.Format("{0}:{1:00}", (int)value / 60, value % 60f);
        }

        public float endTime {
            set => _endTimeText.text = string.Format("{0}:{1:00}", (int)value / 60, value % 60f);
        }

        internal static Material MainUIFontMaterial {
            get {
                if (_mainUIFontMaterial == null) {
                    _mainUIFontMaterial = Resources.FindObjectsOfTypeAll<Material>()
                        .First(m => m.name == "Teko-Medium SDF Curved Softer");
                }

                return _mainUIFontMaterial;
            }
        }

        public void Setup(RectTransform fillBarTransform, RectTransform otherTransform) {
            _otherTransform = otherTransform;
            _rectTransform = transform as RectTransform;
            _fillBarTransform = fillBarTransform;

            _currentTimeText = CreateText();
            _currentTimeText.rectTransform.sizeDelta = fillBarTransform.sizeDelta;
            _currentTimeText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _currentTimeText.alignment = TextAlignmentOptions.Left;
            _currentTimeText.text = "0:00";
            _currentTimeText.name = "Current Time";

            _endTimeText = CreateText();
            _endTimeText.rectTransform.sizeDelta = fillBarTransform.sizeDelta;
            _endTimeText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            _endTimeText.alignment = TextAlignmentOptions.Right;
            _endTimeText.text = "0:00";
            _endTimeText.name = "End Time";
        }

        public void RegisterNode(AmeNode node) {
            node.AddCallback(DragCallback);
        }

        public void UnregisterNode(AmeNode node) {
            node.AddCallback(null);
        }

        private void DragCallback(AmeNode node, Vector2 x, Camera camera) {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, x, camera,
                out Vector2 computedVector);

            if (float.IsNaN(computedVector.x) || float.IsNaN(computedVector.y)) {
                return;
            }

            float computed = computedVector.x;
            float min = XForPercent(Mathf.Clamp(node.min, 0f, 1f));
            float max = XForPercent(Mathf.Clamp(node.max, 0f, 1f));
            if (computed > max || computed < min) {
                return;
            }

            node.transform.localPosition = new Vector3(computed, node.transform.localPosition.y);
            node.SendUpdatePositionCall(PercentForX(computed));
        }

        private float XForPercent(float percent) {
            float maxX = _rectTransform.rect.width;
            return Mathf.Lerp(-maxX, maxX, percent);
        }

        private float PercentForX(float x) {
            float maxX = _rectTransform.rect.width;
            return Mathf.InverseLerp(-maxX, maxX, x);
        }

        public float GetNodePercent(AmeNode node) {
            return PercentForX(node.transform.localPosition.x);
        }

        public void AssignNodeToPercent(AmeNode node, float percent) {
            node.transform.localPosition = new Vector2(XForPercent(percent), node.transform.localPosition.y);
        }

        private CurvedTextMeshPro CreateText() {
            GameObject textGameObject = new GameObject("AmeText");
            textGameObject.transform.SetParent(transform, false);

            CurvedTextMeshPro curvedText = textGameObject.AddComponent<CurvedTextMeshPro>();
            curvedText.font = BeatSaberUI.MainTextFont;
            curvedText.fontSharedMaterial = MainUIFontMaterial;
            curvedText.text = nameof(CurvedTextMeshPro);
            curvedText.rectTransform.anchorMin = Vector2.zero;
            curvedText.rectTransform.anchorMax = Vector2.one;
            return curvedText;
        }
    }
}