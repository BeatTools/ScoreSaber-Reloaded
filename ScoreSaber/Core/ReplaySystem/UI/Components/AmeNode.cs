﻿#region

using System;
using UnityEngine;

#endregion

namespace ScoreSaber.Core.ReplaySystem.UI.Components {
    public class AmeNode : MonoBehaviour {
        private Action<AmeNode, Vector2, Camera> _callback;

        private AmeHandle _handle;
        public bool isBeingDragged => _handle.dragged;

        public bool moveable { get; set; } = true;
        public float min { get; set; }
        public float max { get; set; } = 1f;
        public event Action<float> PositionDidChange;

        internal void Init(AmeHandle handle) {
            _handle = handle;
        }

        public void AddCallback(Action<AmeNode, Vector2, Camera> dragCallback) {
            _callback = dragCallback;
            _handle.AddCallback(Callback);
        }

        private void Callback(AmeHandle handle, Vector2 x, Camera camera) {
            if (moveable) {
                _callback?.Invoke(this, x, camera);
            }
        }

        public void SendUpdatePositionCall(float percentOnBar) {
            PositionDidChange?.Invoke(percentOnBar);
        }
    }
}