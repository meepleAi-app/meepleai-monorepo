# UserLibrary — GameState Transition Diagram

## Stati

| Valore | Descrizione |
|--------|-------------|
| `Nuovo` | Aggiunto alla libreria, non ancora classificato |
| `Owned` | Posseduto dall'utente |
| `InPrestito` | Prestato a qualcuno (`StateNotes` = nome/contatto debitore) |
| `Wishlist` | Desiderato, non posseduto |

## Diagramma transizioni valide

```mermaid
stateDiagram-v2
    [*] --> Nuovo : AddGameToLibrary

    Nuovo --> Owned : MarkAsOwned
    Nuovo --> Wishlist : MarkAsWishlist
    Nuovo --> InPrestito : MarkAsOnLoan

    Owned --> InPrestito : MarkAsOnLoan
    Owned --> Wishlist : MarkAsWishlist

    InPrestito --> Owned : MarkAsReturned
    InPrestito --> Wishlist : MarkAsWishlist

    Wishlist --> Owned : MarkAsOwned
    Wishlist --> InPrestito : MarkAsOnLoan
```

## Regole

- `StateNotes` (nullable string) contiene le info sul debitore quando lo stato è `InPrestito`
- `ChangedAt` (nullable DateTime) traccia l'ultimo cambio di stato
- Le transizioni non valide lanciano `ConflictException`
- `DeclareOwnership` è ortogonale allo stato: può essere chiamata in qualsiasi stato
