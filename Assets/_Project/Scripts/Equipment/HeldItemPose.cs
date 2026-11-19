using UnityEngine;
using Wreckabulary.Art;
using Wreckabulary.Rules;

namespace Wreckabulary
{
    public static class HeldItemPose
    {
        public static Quaternion Turn(ItemDefinition item)
        {
            var turn = ItemHolds.InHand(item);
            return new Quaternion(turn.X, turn.Y, turn.Z, turn.W);
        }

        public static Vector3 Grip(ItemDefinition item)
        {
            var grip = ItemHolds.Grip(item);
            return grip == null || grip.Length < 3 ? Vector3.zero : new Vector3(grip[0], grip[1], grip[2]);
        }

        public static Transform Model(HeldWeapon gear) => Model(gear, out _);

        public static void SeatModel(HeldWeapon gear)
        {
            var hold = ItemHolds.For(gear ? gear.Definition?.Id : null);
            if (hold == null || hold.ModelYaw == 0f && hold.ModelShift == null) return;
            var model = Model(gear, out var source);
            if (!model) return;
            var turn = Quaternion.Euler(0f, hold.ModelYaw, 0f);
            var shift = hold.ModelShift == null ? Vector3.zero : new Vector3(hold.ModelShift[0], hold.ModelShift[1], hold.ModelShift[2]);
            model.SetLocalPositionAndRotation(turn * source.localPosition + shift, turn * source.localRotation);
        }

        static Transform Model(HeldWeapon gear, out Transform source)
        {
            var library = gear && gear.Definition != null ? ModelLibrary.Load() : null;
            var prefab = library ? library.Find(gear.Definition.Model) : null;
            source = prefab ? prefab.transform : null;
            return source ? gear.transform.Find(source.name) : null;
        }
    }
}
