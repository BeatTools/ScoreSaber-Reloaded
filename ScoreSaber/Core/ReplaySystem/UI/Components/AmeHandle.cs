﻿#region

using System;
using UnityEngine;
using UnityEngine.EventSystems;

#endregion

namespace ScoreSaber.Core.ReplaySystem.UI.Components {
    public class AmeHandle : MonoBehaviour, IBeginDragHandler, IDragHandler, IEventSystemHandler,
        IInitializePotentialDragHandler, IEndDragHandler {
        private Action<AmeHandle, Vector2, Camera> _handleMoveCallback;
        public bool dragged { get; private set; }

        public void OnBeginDrag(PointerEventData eventData) {
            dragged = true;
        }

        public void OnDrag(PointerEventData eventData) {
            if (eventData.position.x == 0f) {
                return;
            }

            _handleMoveCallback?.Invoke(this, eventData.position, eventData.pressEventCamera);
        }

        public void OnEndDrag(PointerEventData eventData) {
            dragged = false;
        }

        public void OnInitializePotentialDrag(PointerEventData eventData) {
            dragged = true;
            eventData.useDragThreshold = false;
        }

        public void AddCallback(Action<AmeHandle, Vector2, Camera> callback) {
            _handleMoveCallback = callback;
        }
    }
}