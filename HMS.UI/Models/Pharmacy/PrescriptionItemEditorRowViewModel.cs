namespace HMS.UI.Models.Pharmacy
{
    public class PrescriptionItemEditorRowViewModel
    {
        public int Index { get; set; }
        public CreatePrescriptionItemViewModel Item { get; set; } = new();
        public InventoryItemViewModel[] AvailableInventoryItems { get; set; } = System.Array.Empty<InventoryItemViewModel>();
    }
}
