using ScheduleOne.DevUtilities;
using ScheduleOne.Equipping;
using ScheduleOne.ItemFramework;
using UnityEngine;

namespace ScheduleOne.PlayerScripts;

public class HotbarSlot : ItemSlot
{
	public delegate void EquipEvent(bool equipped);

	public Equippable Equippable;

	public EquipEvent onEquipChanged;

	public bool IsEquipped { get; protected set; }

	public override void SetStoredItem(ItemInstance instance, bool _internal = false)
	{
		if ((_internal || base.SlotOwner == null) && IsEquipped && Equippable != null)
		{
			Equippable.Unequip();
			Equippable = null;
		}
		base.SetStoredItem(instance, _internal);
		if ((_internal || base.SlotOwner == null) && IsEquipped && instance != null && instance.Equippable != null)
		{
			if (PlayerSingleton<PlayerInventory>.Instance.onPreItemEquipped != null)
			{
				PlayerSingleton<PlayerInventory>.Instance.onPreItemEquipped.Invoke();
			}
			Equippable = Object.Instantiate(instance.Equippable.gameObject, PlayerSingleton<PlayerInventory>.Instance.equipContainer).GetComponent<Equippable>();
			Equippable.Equip(instance);
		}
	}

	public override void ClearStoredInstance(bool _internal = false)
	{
		if ((_internal || base.SlotOwner == null) && IsEquipped && Equippable != null)
		{
			Equippable.Unequip();
			Equippable = null;
		}
		base.ClearStoredInstance(_internal);
	}

	public virtual void Equip()
	{
		IsEquipped = true;
		if (base.ItemInstance != null && base.ItemInstance.Equippable != null)
		{
			if (PlayerSingleton<PlayerInventory>.Instance.onPreItemEquipped != null)
			{
				PlayerSingleton<PlayerInventory>.Instance.onPreItemEquipped.Invoke();
			}
			Equippable = Object.Instantiate(base.ItemInstance.Equippable.gameObject, PlayerSingleton<PlayerInventory>.Instance.equipContainer).GetComponent<Equippable>();
			Equippable.Equip(base.ItemInstance);
		}
		if (onEquipChanged != null)
		{
			onEquipChanged(equipped: true);
		}
	}

	public virtual void Unequip()
	{
		if (Equippable != null)
		{
			Equippable.Unequip();
			Equippable = null;
		}
		IsEquipped = false;
		if (onEquipChanged != null)
		{
			onEquipChanged(equipped: false);
		}
	}

	public override bool CanSlotAcceptCash()
	{
		return false;
	}
}
