using NetworkService.Enums;

namespace NetworkService.Models
{
    public class MeracPotrosnje
    {
        public int Id { get; set; }  = 0;
        public string Name { get; set; } = string.Empty;
        public EntityType EntityType { get; set; } = EntityType.INTERVAL_METER;
    }
}
