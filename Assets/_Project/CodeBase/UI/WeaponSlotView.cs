using _Project.CodeBase.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Project.CodeBase.UI
{
    public class WeaponSlotView : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Image background;
        [SerializeField] private Image icon;
        [SerializeField] private TextMeshProUGUI slotNumberText;
        [SerializeField] private TextMeshProUGUI ammoText;
        [Tooltip("Родительский объект панели патронов (тот что с фоном). " +
                 "Включается только когда оружие в руках.")]
        [SerializeField] private GameObject ammoRoot;
 
        [Header("Colors")]
        [SerializeField] private Color emptyColor    = new Color(1f, 1f, 1f, 0.15f);
        [SerializeField] private Color filledColor   = new Color(1f, 1f, 1f, 0.35f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.85f, 0.3f, 0.9f);
 
        [Header("Low Ammo")]
        [Tooltip("Ниже этой доли от максимума патроны краснеют")]
        [SerializeField] private float lowAmmoThreshold = 0.25f;
        [SerializeField] private Color lowAmmoColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField] private Color normalAmmoColor = Color.white;
 
        private int _slotIndex;
 
        public void Setup(int slotIndex)
        {
            _slotIndex = slotIndex;
 
            if (slotNumberText != null)
                slotNumberText.text = (slotIndex + 1).ToString();
 
            ShowEmpty();
        }
 
        public void ShowEmpty()
        {
            if (background != null)
                background.color = emptyColor;
 
            if (icon != null)
                icon.enabled = false;
 
            SetAmmoPanelVisible(false);
        }
 
        /// <summary>
        /// ammo: -1 = бесконечные, иначе количество
        /// </summary>
        /// <summary>
        /// ammo: -1 = бесконечные, иначе количество
        /// </summary>
        public void ShowWeapon(WeaponConfig weapon, int ammo, bool isSelected)
        {
            if (weapon == null)
            {
                ShowEmpty();
                return;
            }
 
            if (background != null)
                background.color = isSelected ? selectedColor : filledColor;
 
            if (icon != null)
            {
                icon.enabled = weapon.Icon != null;
                icon.sprite = weapon.Icon;
            }
 
            // Патроны показываем только для оружия в руках
            SetAmmoPanelVisible(isSelected);
 
            if (isSelected)
                UpdateAmmoText(weapon, ammo);
        }
        
        private void SetAmmoPanelVisible(bool visible)
        {
            // Если ammoRoot не назначен — работаем по старому,
            // просто чистим текст
            if (ammoRoot == null)
            {
                if (!visible && ammoText != null)
                    ammoText.text = string.Empty;
 
                return;
            }
 
            ammoRoot.SetActive(visible);
        }
 
        private void UpdateAmmoText(WeaponConfig weapon, int ammo)
        {
            if (ammoText == null)
                return;
 
            if (ammo < 0)
            {
                // Бесконечные патроны — знак бесконечности
                ammoText.text = "\u221E";
                ammoText.color = normalAmmoColor;
                return;
            }
 
            ammoText.text = ammo.ToString();
 
            float max = Mathf.Max(1, weapon.AmmoOnPickup);
            bool isLow = ammo / max <= lowAmmoThreshold;
 
            ammoText.color = isLow ? lowAmmoColor : normalAmmoColor;
        }
    }
}