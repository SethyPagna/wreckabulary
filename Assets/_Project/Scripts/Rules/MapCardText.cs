using System.Linq;

namespace Wreckabulary.Rules
{
    public static class MapCardText
    {
        public const string Separator = " · ";

        public static string HouseLine(int floors, int rooms, string outdoors)
        {
            string line = Count(floors, "floor") + Separator + Count(rooms, "room");
            return string.IsNullOrEmpty(outdoors) ? line : line + Separator + outdoors;
        }

        public static string HouseLine(HouseLayout layout)
        {
            if (layout == null || layout.Rooms.Count == 0) return "";
            int rooms = layout.Rooms.Count(room => !room.Outdoor && !IsPassage(room.Name));
            return HouseLine(layout.StoreyFloors().Count, rooms, Outdoors(layout));
        }

        public static string Outdoors(HouseLayout layout)
        {
            var outside = layout.Rooms.Where(room => room.Outdoor).ToList();
            if (outside.Count == 0) return "";
            return outside.Any(room => layout.StoreyOf(room) == 0) ? "garden" : "roof garden";
        }

        public static bool IsPassage(string room) => room != null && (room.Contains("Hall") || room.Contains("Landing"));

        static string Count(int value, string noun) => value + " " + noun + (value == 1 ? "" : "s");
    }
}
