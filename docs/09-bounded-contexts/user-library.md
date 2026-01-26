# UserLibrary Bounded Context

**Collezioni giochi utente, wishlist, played history**

---

## 📋 Responsabilità

- Collezione personale giochi
- Wishlist (giochi desiderati)
- Played history (partite registrate)
- Statistiche personali (giochi preferiti, tempo totale)

---

## 🏗️ Domain Model

**UserLibrary** (Aggregate Root):
```csharp
public class UserLibrary
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public List<LibraryGame> Games { get; private set; }
    public List<WishlistGame> Wishlist { get; private set; }

    public void AddGame(Guid gameId) { }
    public void RemoveGame(Guid gameId) { }
    public void AddToWishlist(Guid gameId) { }
}
```

**LibraryGame**:
```csharp
public class LibraryGame
{
    public Guid GameId { get; private set; }
    public DateTime AddedAt { get; private set; }
    public int TimesPlayed { get; private set; }

    public void RecordPlay() { }
}
```

---

## 📡 Application Layer

### Commands
- `AddGameToLibraryCommand`
- `RemoveGameFromLibraryCommand`
- `AddToWishlistCommand`
- `MoveToLibraryFromWishlistCommand`

### Queries
- `GetUserLibraryQuery`
- `GetWishlistQuery`
- `GetLibraryStatsQuery`

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/UserLibrary/`

---

**Status**: ✅ Production
**Last Updated**: 2026-01-18
