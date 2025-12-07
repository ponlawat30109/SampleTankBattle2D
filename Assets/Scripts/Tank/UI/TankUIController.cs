using System;
using UnityEngine;
using UnityEngine.UIElements;
using FishNet.Object;

namespace TankBattle.Tank.UI
{
    [RequireComponent(typeof(UIDocument))]
    public class TankUIController : MonoBehaviour
    {
        private readonly string hpBarName = "bg";

        private UIDocument _uiDoc;
        private VisualElement _hpBar;

        private TankCore _tankCore;

        void Awake()
        {
            _uiDoc = GetComponent<UIDocument>();
        }

        void OnEnable()
        {
            if (_uiDoc == null) return;

            var root = _uiDoc.rootVisualElement;
            if (root == null) return;

            _hpBar = root.Q<VisualElement>(hpBarName);

            _tankCore = GetComponentInParent<TankCore>();

            if (_tankCore != null)
            {
                _tankCore.OnHPChanged += OnHPChanged;
                OnHPChanged(_tankCore.HP);
            }
        }

        void OnDisable()
        {
            if (_tankCore != null)
                _tankCore.OnHPChanged -= OnHPChanged;
        }

        private void OnHPChanged(float hp)
        {
            float maxHP = 100f;
            if (_tankCore != null)
                maxHP = Mathf.Max(1, _tankCore.MaxHP);

            float pct = Mathf.Clamp01(hp / maxHP);
            if (_hpBar != null)
            {
                _hpBar.style.width = new StyleLength(new Length(pct * 100f, LengthUnit.Percent));
            }
        }
    }
}
