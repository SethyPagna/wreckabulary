namespace Wreckabulary
{
    public static class WordboundQuickPlayEntry
    {
        static bool pending, returned;

        public static void Request()
        {
            WordboundSoloEntry.CancelRequest();
            WordboundWorkshopEntry.CancelRequest();
            WordboundRoomEntry.CancelRequest();
            pending = true;
        }

        public static bool TakeRequest()
        {
            bool result = pending;
            pending = false;
            return result;
        }

        public static void CancelRequest() => pending = false;

        public static void MarkReturned() => returned = true;

        public static void CancelReturn() => returned = false;

        public static bool TakeReturn()
        {
            bool result = returned;
            returned = false;
            return result;
        }
    }
}
