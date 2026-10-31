namespace Cthangover.Tools.EntityEditor.Tests
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>A small entity DTO for <see cref="EntityJsonTests"/>: a default-valued number and extension data.</summary>
    public class SampleEntity
    {
        /// <summary>Id.</summary>
        public string Id { get; set; }

        /// <summary>Price; 0 when not written.</summary>
        public int Cost { get; set; }

        /// <summary>Picture.</summary>
        public string Sprite { get; set; }

        /// <summary>Properties of other mods.</summary>
        [JsonExtensionData]
        public Dictionary<string, object> Extra { get; set; }
    }
}
