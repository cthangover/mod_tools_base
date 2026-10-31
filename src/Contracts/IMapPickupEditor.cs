namespace Cthangover.Tools.Contracts
{
    /// <summary>
    /// Optional capability of the RPG map editor (<c>rpg_editor</c>): the item editor's "Place on map" asks it to
    /// open a map of the item's mod and to choose a pickup object in its palette, so the author places the pickup
    /// at once. The editor does not know items: it picks, from the objects of category <c>pickup</c>, the one whose
    /// id ends like the item's last id segment; without one it returns null, and the item editor writes a pickup
    /// object from the item's picture and asks again.
    /// </summary>
    public interface IMapPickupEditor
    {
        /// <summary>
        /// Opens <paramref name="mapPath"/> (a file of <paramref name="modId"/>) and makes the pickup for
        /// <paramref name="itemId"/> the object to place. Returns the chosen object's id, or null when there is none.
        /// </summary>
        string PreparePickup(string modId, string mapPath, string itemId);
    }
}
