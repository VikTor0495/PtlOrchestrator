namespace PtlController.Domain;

public enum CartAssignmentType
{
    ExistingItem,     // articolo già presente
    NewItem,          // nuovo articolo su carrello libero
    CartFull,         // nessun carrello disponibile
}
