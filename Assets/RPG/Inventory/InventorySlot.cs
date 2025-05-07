namespace RPG.Inventory
{
    public class InventorySlot
    {
        public InventoryItem item;
        public int quantity;

        public InventorySlot(InventoryItem item = null, int quantity = 0)
        {
            this.item = item;
            this.quantity = quantity;
        }

        public void Clear()
        {
            item = null;
            quantity = 0;
        }

        public bool IsEmpty()
        {
            return item == null || quantity == 0;
        }
    }
}