namespace Api.BoundedContexts.UserLibrary.Domain.ValueObjects;

/// <summary>
/// Visibility level for wishlist items.
/// Issue #3917: Wishlist sharing feature.
/// </summary>
public enum WishlistVisibility
{
    Private = 0,
    Friends = 1,
    Public = 2
}
