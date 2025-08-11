using System;
using System.Collections.Generic;
using System.Linq;
using ScheduleOne.DevUtilities;
using ScheduleOne.Equipping;
using ScheduleOne.ItemFramework;
using ScheduleOne.Money;
using ScheduleOne.Product;
using ScheduleOne.Product.Packaging;
using ScheduleOne.UI;
using ScheduleOne.UI.Items;
using ScheduleOne.Variables;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ScheduleOne.PlayerScripts;

public class PlayerInventory : PlayerSingleton<PlayerInventory>
{
	[Serializable]
	public class ItemVariable
	{
		public ItemDefinition Definition;

		public string VariableName;
	}

	[Serializable]
	private class ItemAmount
	{
		public ItemDefinition Definition;

		public int Amount = 10;
	}

	public const float LABEL_DISPLAY_TIME = 2f;

	public const float LABEL_FADE_TIME = 0.5f;

	public const float DISCARD_TIME = 1.5f;

	public const int INVENTORY_SLOT_COUNT = 8;

	[Header("Startup Items (Editor only)")]
	[SerializeField]
	private bool giveStartupItems;

	[SerializeField]
	private List<ItemAmount> startupItems = new List<ItemAmount>();

	[Header("References")]
	public Transform equipContainer;

	public List<HotbarSlot> hotbarSlots = new List<HotbarSlot>();

	private ClipboardSlot clipboardSlot;

	private List<ItemSlotUI> slotUIs = new List<ItemSlotUI>();

	private ItemSlot discardSlot;

	[Header("Item Variables")]
	public List<ItemVariable> ItemVariables = new List<ItemVariable>();

	public Action<bool> onInventoryStateChanged;

	private int PriorEquippedSlotIndex = -1;

	private int PreviousEquippedSlotIndex = -1;

	public UnityEvent onPreItemEquipped;

	public UnityEvent onItemEquipped;

	private bool ManagementSlotEnabled;

	public float currentEquipTime;

	protected float currentDiscardTime;

	public int TOTAL_SLOT_COUNT => 9 + (ManagementSlotEnabled ? 1 : 0);

	public CashSlot cashSlot { get; private set; }

	public CashInstance cashInstance { get; protected set; }

	public int EquippedSlotIndex { get; protected set; } = -1;

	public bool HotbarEnabled { get; protected set; } = true;

	public bool EquippingEnabled { get; protected set; } = true;

	public Equippable equippable { get; protected set; }

	public HotbarSlot equippedSlot
	{
		get
		{
			if (EquippedSlotIndex == -1)
			{
				return null;
			}
			return IndexAllSlots(EquippedSlotIndex);
		}
	}

	public bool isAnythingEquipped
	{
		get
		{
			if (equippedSlot == null)
			{
				return false;
			}
			return equippedSlot.ItemInstance != null;
		}
	}

	public HotbarSlot IndexAllSlots(int index)
	{
		if (index < 0)
		{
			return null;
		}
		if (ManagementSlotEnabled)
		{
			if (index < hotbarSlots.Count)
			{
				return hotbarSlots[index];
			}
			return index switch
			{
				8 => clipboardSlot, 
				9 => cashSlot, 
				_ => null, 
			};
		}
		if (index < hotbarSlots.Count)
		{
			return hotbarSlots[index];
		}
		if (index == 8)
		{
			return cashSlot;
		}
		return null;
	}

	protected override void Awake()
	{
		base.Awake();
		SetupInventoryUI();
	}

	private void SetupInventoryUI()
	{
		for (int i = 0; i < 8; i++)
		{
			HotbarSlot hotbarSlot = new HotbarSlot();
			hotbarSlots.Add(hotbarSlot);
			ItemSlotUI component = UnityEngine.Object.Instantiate(Singleton<ItemUIManager>.Instance.HotbarSlotUIPrefab, Singleton<HUD>.Instance.SlotContainer).GetComponent<ItemSlotUI>();
			component.AssignSlot(hotbarSlot);
			slotUIs.Add(component);
		}
		cashSlot = new CashSlot();
		cashSlot.SetStoredItem(Registry.GetItem("cash").GetDefaultInstance());
		cashInstance = cashSlot.ItemInstance as CashInstance;
		cashSlot.AddFilter(new ItemFilter_Category(new List<EItemCategory> { EItemCategory.Cash }));
		Singleton<HUD>.Instance.cashSlotUI.GetComponent<CashSlotUI>().AssignSlot(cashSlot);
		slotUIs.Add(Singleton<HUD>.Instance.cashSlotUI.GetComponent<ItemSlotUI>());
		discardSlot = new ItemSlot();
		Singleton<HUD>.Instance.discardSlot.AssignSlot(discardSlot);
		RepositionUI();
	}

	private void RepositionUI()
	{
		float num = 0f;
		float num2 = 20f;
		for (int i = 0; i < 8; i++)
		{
			ItemSlotUI itemSlotUI = slotUIs[i];
			itemSlotUI.Rect.Find("Background/Index").GetComponent<TextMeshProUGUI>().text = ((i + 1) % 10).ToString();
			itemSlotUI.Rect.anchoredPosition = new Vector2(num + itemSlotUI.Rect.sizeDelta.x / 2f + num2, 0f);
			num += itemSlotUI.Rect.sizeDelta.x + num2;
			if (i == 7)
			{
				itemSlotUI.Rect.Find("Seperator").gameObject.SetActive(value: true);
				itemSlotUI.Rect.Find("Seperator").GetComponent<RectTransform>().anchoredPosition = new Vector2(num2, 0f);
				num += num2;
			}
		}
		int num3 = 8;
		if (ManagementSlotEnabled)
		{
			Singleton<HUD>.Instance.managementSlotUI.transform.Find("Background/Index").GetComponent<Text>().text = ((num3 + 1) % 10).ToString();
			Singleton<HUD>.Instance.managementSlotContainer.anchoredPosition = new Vector2(num + Singleton<HUD>.Instance.managementSlotContainer.sizeDelta.x / 2f + num2, 0f);
			num += Singleton<HUD>.Instance.managementSlotContainer.sizeDelta.x + num2;
			num3++;
		}
		Singleton<HUD>.Instance.managementSlotContainer.gameObject.SetActive(ManagementSlotEnabled);
		Singleton<HUD>.Instance.cashSlotUI.Find("Background/Index").GetComponent<Text>().text = ((num3 + 1) % 10).ToString();
		Singleton<HUD>.Instance.cashSlotContainer.anchoredPosition = new Vector2(num + Singleton<HUD>.Instance.cashSlotContainer.sizeDelta.x / 2f + num2, 0f);
		num += Singleton<HUD>.Instance.cashSlotContainer.sizeDelta.x + num2;
		Singleton<HUD>.Instance.SlotContainer.anchoredPosition = new Vector2((0f - num) / 2f, Singleton<HUD>.Instance.SlotContainer.anchoredPosition.y);
	}

	protected override void Start()
	{
		base.Start();
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			HotbarSlot slot = hotbarSlots[i];
			Player.Local.SetInventoryItem(i, slot.ItemInstance);
			int index = i;
			HotbarSlot hotbarSlot = slot;
			hotbarSlot.onItemDataChanged = (Action)Delegate.Combine(hotbarSlot.onItemDataChanged, (Action)delegate
			{
				UpdateInventoryVariables();
				Player.Local.SetInventoryItem(index, slot.ItemInstance);
			});
		}
		Player.Local.SetInventoryItem(8, cashSlot.ItemInstance);
		CashSlot obj = cashSlot;
		obj.onItemDataChanged = (Action)Delegate.Combine(obj.onItemDataChanged, (Action)delegate
		{
			UpdateInventoryVariables();
			Player.Local.SetInventoryItem(8, cashSlot.ItemInstance);
		});
		if (giveStartupItems)
		{
			GiveStartupItems();
		}
		(NetworkSingleton<VariableDatabase>.Instance.GetVariable("ClipboardAcquired") as BoolVariable).OnValueChanged.AddListener(ClipboardAcquiredVarChange);
	}

	private void GiveStartupItems()
	{
		if (!Application.isEditor && !Debug.isDebugBuild)
		{
			return;
		}
		foreach (ItemAmount startupItem in startupItems)
		{
			AddItemToInventory(startupItem.Definition.GetDefaultInstance(startupItem.Amount));
		}
	}

	protected virtual void Update()
	{
		UpdateHotbarSelection();
		if (isAnythingEquipped && HotbarEnabled)
		{
			currentEquipTime += Time.deltaTime;
		}
		else
		{
			currentEquipTime = 0f;
		}
		if (isAnythingEquipped)
		{
			Singleton<HUD>.Instance.selectedItemLabel.text = equippedSlot.ItemInstance.Name;
			Singleton<HUD>.Instance.selectedItemLabel.color = equippedSlot.ItemInstance.LabelDisplayColor;
			if (currentEquipTime > 2f)
			{
				float num = Mathf.Clamp01((currentEquipTime - 2f) / 0.5f);
				Singleton<HUD>.Instance.selectedItemLabel.color = new Color(Singleton<HUD>.Instance.selectedItemLabel.color.r, Singleton<HUD>.Instance.selectedItemLabel.color.g, Singleton<HUD>.Instance.selectedItemLabel.color.b, 1f - num);
			}
			else
			{
				Singleton<HUD>.Instance.selectedItemLabel.color = new Color(Singleton<HUD>.Instance.selectedItemLabel.color.r, Singleton<HUD>.Instance.selectedItemLabel.color.g, Singleton<HUD>.Instance.selectedItemLabel.color.b, 1f);
			}
		}
		else
		{
			Singleton<HUD>.Instance.selectedItemLabel.text = string.Empty;
		}
		if (discardSlot.ItemInstance != null && !Singleton<HUD>.Instance.discardSlot.IsBeingDragged)
		{
			currentDiscardTime += Time.deltaTime;
			Singleton<HUD>.Instance.discardSlotFill.fillAmount = currentDiscardTime / 1.5f;
			if (currentDiscardTime >= 1.5f)
			{
				discardSlot.ClearStoredInstance();
			}
		}
		else
		{
			currentDiscardTime = 0f;
			Singleton<HUD>.Instance.discardSlotFill.fillAmount = 0f;
		}
	}

	private void UpdateHotbarSelection()
	{
		if (!HotbarEnabled || !EquippingEnabled || GameInput.IsTyping || Singleton<PauseMenu>.Instance.IsPaused)
		{
			return;
		}
		int num = -1;
		if (Input.GetKeyDown(KeyCode.Alpha1))
		{
			num = 0;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha2))
		{
			num = 1;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha3))
		{
			num = 2;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha4))
		{
			num = 3;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha5))
		{
			num = 4;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha6))
		{
			num = 5;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha7))
		{
			num = 6;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha8))
		{
			num = 7;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha9))
		{
			num = 8;
		}
		else if (Input.GetKeyDown(KeyCode.Alpha0))
		{
			num = 9;
		}
		if (num == -1)
		{
			float mouseScrollDelta = GameInput.MouseScrollDelta;
			if (mouseScrollDelta < 0f)
			{
				num = EquippedSlotIndex + 1;
				if (num >= TOTAL_SLOT_COUNT)
				{
					num = 0;
				}
			}
			else if (mouseScrollDelta > 0f)
			{
				num = EquippedSlotIndex - 1;
				if (num < 0)
				{
					num = TOTAL_SLOT_COUNT - 1;
				}
			}
		}
		if (num == -1 && GameInput.GetButtonDown(GameInput.ButtonCode.TertiaryClick))
		{
			if (EquippedSlotIndex != -1)
			{
				num = EquippedSlotIndex;
			}
			else if (PreviousEquippedSlotIndex != -1)
			{
				num = PreviousEquippedSlotIndex;
			}
		}
		if (num != -1 && num < TOTAL_SLOT_COUNT)
		{
			if (num != EquippedSlotIndex && EquippedSlotIndex != -1)
			{
				IndexAllSlots(EquippedSlotIndex).Unequip();
				currentEquipTime = 0f;
			}
			PreviousEquippedSlotIndex = EquippedSlotIndex;
			EquippedSlotIndex = -1;
			if (IndexAllSlots(num).IsEquipped)
			{
				IndexAllSlots(num).Unequip();
				return;
			}
			Equip(IndexAllSlots(num));
			EquippedSlotIndex = num;
			PlayerSingleton<ViewmodelSway>.Instance.RefreshViewmodel();
		}
	}

	public void Equip(HotbarSlot slot)
	{
		slot.Equip();
	}

	public void SetInventoryEnabled(bool enabled)
	{
		HotbarEnabled = enabled;
		if (onInventoryStateChanged != null)
		{
			onInventoryStateChanged(enabled);
		}
		Singleton<HUD>.Instance.HotbarContainer.gameObject.SetActive(enabled);
		SetEquippingEnabled(enabled);
	}

	public void SetEquippingEnabled(bool enabled)
	{
		if (EquippingEnabled == enabled)
		{
			return;
		}
		EquippingEnabled = enabled;
		equipContainer.gameObject.SetActive(enabled);
		if (enabled)
		{
			if (PriorEquippedSlotIndex != -1)
			{
				EquippedSlotIndex = PriorEquippedSlotIndex;
				Equip(IndexAllSlots(EquippedSlotIndex));
			}
		}
		else
		{
			PriorEquippedSlotIndex = EquippedSlotIndex;
			if (EquippedSlotIndex != -1)
			{
				IndexAllSlots(EquippedSlotIndex).Unequip();
				EquippedSlotIndex = -1;
			}
		}
		foreach (ItemSlotUI slotUI in slotUIs)
		{
			slotUI.Rect.Find("Background/Index").gameObject.SetActive(enabled);
		}
	}

	private void ClipboardAcquiredVarChange(bool newVal)
	{
		SetManagementClipboardEnabled(newVal);
	}

	public void SetManagementClipboardEnabled(bool enabled)
	{
		enabled = false;
		ManagementSlotEnabled = enabled;
		RepositionUI();
	}

	public void SetViewmodelVisible(bool visible)
	{
		PlayerSingleton<PlayerCamera>.Instance.Camera.cullingMask = (visible ? (PlayerSingleton<PlayerCamera>.Instance.Camera.cullingMask | (1 << LayerMask.NameToLayer("Viewmodel"))) : (PlayerSingleton<PlayerCamera>.Instance.Camera.cullingMask & ~(1 << LayerMask.NameToLayer("Viewmodel"))));
	}

	public bool CanItemFitInInventory(ItemInstance item, int quantity = 1)
	{
		if (item == null)
		{
			Console.LogWarning("CanItemFitInInventory: item is null!");
			return false;
		}
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			if (hotbarSlots[i].ItemInstance == null)
			{
				quantity -= item.StackLimit;
			}
			else if (hotbarSlots[i].ItemInstance.CanStackWith(item))
			{
				quantity -= item.StackLimit - hotbarSlots[i].ItemInstance.Quantity;
			}
		}
		return quantity <= 0;
	}

	public void AddItemToInventory(ItemInstance item)
	{
		if (item == null)
		{
			Console.LogError("AddItemToInventory: item is null!");
			return;
		}
		if (!item.IsValidInstance())
		{
			Console.LogError("AddItemToInventory: item is not valid!");
			return;
		}
		if (!CanItemFitInInventory(item))
		{
			Console.LogWarning("AddItemToInventory: item won't fit!");
			return;
		}
		int num = item.Quantity;
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			if (num == 0)
			{
				break;
			}
			if (hotbarSlots[i].ItemInstance != null && hotbarSlots[i].ItemInstance.CanStackWith(item, checkQuantities: false))
			{
				int num2 = Mathf.Min(num, hotbarSlots[i].ItemInstance.StackLimit - hotbarSlots[i].Quantity);
				if (num2 > 0)
				{
					hotbarSlots[i].ChangeQuantity(num2);
					num -= num2;
				}
			}
		}
		for (int j = 0; j < hotbarSlots.Count; j++)
		{
			if (num == 0)
			{
				break;
			}
			if (hotbarSlots[j].ItemInstance == null)
			{
				hotbarSlots[j].SetStoredItem(item.GetCopy(num));
				num = 0;
			}
		}
		if (num > 0)
		{
			Console.LogWarning("Could not add full amount of '" + item.Name + "' to inventory!");
		}
	}

	public uint GetAmountOfItem(string ID)
	{
		uint num = 0u;
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			if (hotbarSlots[i].ItemInstance != null && hotbarSlots[i].ItemInstance.ID.ToLower() == ID.ToLower())
			{
				num += (uint)hotbarSlots[i].Quantity;
			}
		}
		return num;
	}

	public void RemoveAmountOfItem(string ID, uint amount = 1u)
	{
		uint num = amount;
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			if (hotbarSlots[i].ItemInstance != null && hotbarSlots[i].ItemInstance.ID.ToLower() == ID.ToLower())
			{
				uint num2 = num;
				if (num2 > hotbarSlots[i].Quantity)
				{
					num2 = (uint)hotbarSlots[i].Quantity;
				}
				num -= num2;
				hotbarSlots[i].ChangeQuantity((int)(0 - num2));
				if (num == 0)
				{
					break;
				}
			}
		}
		if (num != 0)
		{
			Console.LogWarning("Could not fully remove " + amount + " " + ID);
		}
	}

	public void ClearInventory()
	{
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			if (hotbarSlots[i].ItemInstance != null)
			{
				hotbarSlots[i].ClearStoredInstance();
			}
		}
	}

	public void RemoveProductFromInventory(EStealthLevel maxStealth)
	{
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			if (hotbarSlots[i].ItemInstance != null && hotbarSlots[i].ItemInstance is ProductItemInstance)
			{
				ProductItemInstance productItemInstance = hotbarSlots[i].ItemInstance as ProductItemInstance;
				EStealthLevel eStealthLevel = EStealthLevel.None;
				if (productItemInstance.AppliedPackaging != null)
				{
					eStealthLevel = productItemInstance.AppliedPackaging.StealthLevel;
				}
				if (eStealthLevel <= maxStealth)
				{
					hotbarSlots[i].ClearStoredInstance();
				}
			}
		}
	}

	public void RemoveRandomItemsFromInventory()
	{
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			if (hotbarSlots[i].ItemInstance != null && UnityEngine.Random.Range(0, 3) == 0)
			{
				int num = UnityEngine.Random.Range(1, hotbarSlots[i].ItemInstance.Quantity + 1);
				hotbarSlots[i].ChangeQuantity(-num);
			}
		}
	}

	public void SetEquippable(Equippable eq)
	{
		equippable = eq;
		if (equippable != null && onItemEquipped != null)
		{
			onItemEquipped.Invoke();
		}
	}

	public void Reequip()
	{
		HotbarSlot hotbarSlot = equippedSlot;
		if (hotbarSlot != null)
		{
			hotbarSlot.Unequip();
			currentEquipTime = 0f;
			Equip(hotbarSlot);
		}
	}

	public List<ItemSlot> GetAllInventorySlots()
	{
		List<ItemSlot> list = new List<ItemSlot>();
		for (int i = 0; i < hotbarSlots.Count; i++)
		{
			list.Add(hotbarSlots[i]);
		}
		list.Add(cashSlot);
		return list;
	}

	private void UpdateInventoryVariables()
	{
		if (!NetworkSingleton<VariableDatabase>.InstanceExists)
		{
			return;
		}
		int num = 0;
		int num2 = 0;
		for (int i = 0; i < ItemVariables.Count; i++)
		{
			int num3 = 0;
			for (int j = 0; j < hotbarSlots.Count; j++)
			{
				if (hotbarSlots[j].ItemInstance != null && hotbarSlots[j].ItemInstance.ID.ToLower() == ItemVariables[i].Definition.ID.ToLower())
				{
					num3 += hotbarSlots[j].Quantity;
				}
				if (hotbarSlots[j].ItemInstance != null && NetworkSingleton<ProductManager>.Instance.ValidMixIngredients.Contains(hotbarSlots[j].ItemInstance.Definition))
				{
					num += hotbarSlots[j].Quantity;
				}
			}
			NetworkSingleton<VariableDatabase>.Instance.SetVariableValue(ItemVariables[i].VariableName, num3.ToString(), network: false);
		}
		int num4 = 0;
		for (int k = 0; k < hotbarSlots.Count; k++)
		{
			if (hotbarSlots[k].ItemInstance != null && hotbarSlots[k].ItemInstance is ProductItemInstance)
			{
				if (hotbarSlots[k].ItemInstance is ProductItemInstance && (hotbarSlots[k].ItemInstance as ProductItemInstance).AppliedPackaging != null)
				{
					num4 += hotbarSlots[k].Quantity;
				}
				if (hotbarSlots[k].ItemInstance is WeedInstance)
				{
					num2 += hotbarSlots[k].Quantity;
				}
			}
		}
		NetworkSingleton<VariableDatabase>.Instance.SetVariableValue("Inventory_Weed_Count", num2.ToString(), network: false);
		NetworkSingleton<VariableDatabase>.Instance.SetVariableValue("Inventory_Packaged_Product", num4.ToString(), network: false);
		NetworkSingleton<VariableDatabase>.Instance.SetVariableValue("Inventory_MixingIngredients", num.ToString(), network: false);
	}
}
