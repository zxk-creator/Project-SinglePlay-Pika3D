
// BagPanel中最下面那一个显示装备的什么背包的那个slot
public class BagSlot : InventoryItem
{
    public BagSlot(BagpackBase bagpack, BagPanel bagPanelRef, EOperateLocation owner)
        : base(bagpack, bagPanelRef, owner, null)
    {
    }
}