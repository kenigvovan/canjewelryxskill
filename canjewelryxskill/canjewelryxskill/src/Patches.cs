using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Config;

namespace canjewelryxskill
{
    public static class CANJewelryXskillPatches
    {
        public static void Postfix_GetHeldItemInfo(ItemSlot inSlot, StringBuilder dsc)
        {
            if (inSlot?.Itemstack?.Attributes?.GetBool("wasExtracted", false) == true)
                dsc.AppendLine(Lang.Get("canjewelryxskill:gem-was-extracted"));
        }
    }
}
