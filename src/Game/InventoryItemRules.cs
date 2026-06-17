using GServ.Protocol;

namespace GServ.Game;

public sealed class DurablePlayerInventoryState
{
    public int Rupees { get; set; }
    public byte Bombs { get; set; }
    public byte Arrows { get; set; }
    public float Hitpoints { get; set; }
    public byte MaxPower { get; set; }
    public byte GlovePower { get; set; }
    public byte ShieldPower { get; set; }
    public byte SwordPower { get; set; }
    public PlayerStatus Status { get; set; }
    public ISet<string> Weapons { get; } = new HashSet<string>(StringComparer.Ordinal);
}

public static class InventoryItemRules
{
    public static byte[] BuildPickupPlayerProps(LevelItemType itemType, DurablePlayerInventoryState player)
    {
        var writer = new GraalBinaryWriter();
        switch (itemType)
        {
            case LevelItemType.GreenRupee:
            case LevelItemType.BlueRupee:
            case LevelItemType.RedRupee:
            case LevelItemType.GoldRupee:
                writer.WriteGChar((byte)PlayerPropertyId.RupeesCount);
                writer.WriteGInt((uint)Math.Clamp(player.Rupees + LevelItemCatalog.GetRupeeCount(itemType), 0, 9_999_999));
                return writer.ToArray();

            case LevelItemType.Bombs:
                writer.WriteGChar((byte)PlayerPropertyId.BombsCount);
                writer.WriteGChar((byte)Math.Clamp(player.Bombs + 5, 0, 99));
                return writer.ToArray();

            case LevelItemType.Darts:
                writer.WriteGChar((byte)PlayerPropertyId.ArrowsCount);
                writer.WriteGChar((byte)Math.Clamp(player.Arrows + 5, 0, 99));
                return writer.ToArray();

            case LevelItemType.Heart:
                writer.WriteGChar((byte)PlayerPropertyId.CurrentPower);
                writer.WriteGChar((byte)(Math.Clamp(player.Hitpoints + 1.0f, 0.0f, player.MaxPower) * 2.0f));
                return writer.ToArray();

            case LevelItemType.Glove1:
            case LevelItemType.Glove2:
                writer.WriteGChar((byte)PlayerPropertyId.GlovePower);
                writer.WriteGChar(NextGlovePower(itemType, player.GlovePower));
                return writer.ToArray();

            case LevelItemType.Bow:
            case LevelItemType.Bomb:
            case LevelItemType.SuperBomb:
            case LevelItemType.Fireball:
            case LevelItemType.Fireblast:
            case LevelItemType.Nukeshot:
            case LevelItemType.JoltBomb:
                player.Weapons.Add(LevelItemCatalog.GetItemName(itemType));
                return [];

            case LevelItemType.Shield:
            case LevelItemType.MirrorShield:
            case LevelItemType.LizardShield:
                writer.WriteGChar((byte)PlayerPropertyId.ShieldPower);
                writer.WriteGChar(NextShieldPower(itemType, player.ShieldPower));
                return writer.ToArray();

            case LevelItemType.Sword:
            case LevelItemType.BattleAxe:
            case LevelItemType.LizardSword:
            case LevelItemType.GoldenSword:
                writer.WriteGChar((byte)PlayerPropertyId.SwordPower);
                writer.WriteGChar(NextSwordPower(itemType, player.SwordPower));
                return writer.ToArray();

            case LevelItemType.FullHeart:
                var heartMax = (byte)Math.Clamp(player.MaxPower + 1, 0, 20);
                writer.WriteGChar((byte)PlayerPropertyId.MaxPower);
                writer.WriteGChar(heartMax);
                writer.WriteGChar((byte)PlayerPropertyId.CurrentPower);
                writer.WriteGChar((byte)(heartMax * 2));
                return writer.ToArray();

            case LevelItemType.SpinAttack:
                if ((player.Status & PlayerStatus.HasSpin) != 0)
                    return [];
                writer.WriteGChar((byte)PlayerPropertyId.Status);
                writer.WriteGChar((byte)(player.Status | PlayerStatus.HasSpin));
                return writer.ToArray();

            default:
                return [];
        }
    }

    public static bool TryRemoveForPlayerDrop(LevelItemType itemType, DurablePlayerInventoryState player)
    {
        switch (itemType)
        {
            case LevelItemType.GreenRupee:
            case LevelItemType.BlueRupee:
            case LevelItemType.RedRupee:
            case LevelItemType.GoldRupee:
                var rupeesRequired = LevelItemCatalog.GetRupeeCount(itemType);
                if (player.Rupees < rupeesRequired)
                    return false;
                player.Rupees -= rupeesRequired;
                return true;

            case LevelItemType.Bombs:
                if (player.Bombs < 5)
                    return false;
                player.Bombs -= 5;
                return true;

            case LevelItemType.Darts:
                if (player.Arrows < 5)
                    return false;
                player.Arrows -= 5;
                return true;

            case LevelItemType.Heart:
                if (player.Hitpoints <= 1.0f)
                    return false;
                player.Hitpoints -= 1.0f;
                return true;

            case LevelItemType.Glove1:
            case LevelItemType.Glove2:
                if (player.GlovePower <= 1)
                    return false;
                player.GlovePower--;
                return true;

            case LevelItemType.SpinAttack:
                if ((player.Status & PlayerStatus.HasSpin) == 0)
                    return false;
                player.Status &= ~PlayerStatus.HasSpin;
                return true;

            default:
                return false;
        }
    }

    public static void ApplyPickupPlayerProps(ReadOnlySpan<byte> payload, DurablePlayerInventoryState player)
    {
        var reader = new GraalBinaryReader(payload);
        while (reader.BytesLeft > 0)
        {
            var propertyId = (PlayerPropertyId)reader.ReadGChar();
            switch (propertyId)
            {
                case PlayerPropertyId.RupeesCount:
                    player.Rupees = reader.ReadGInt();
                    break;

                case PlayerPropertyId.BombsCount:
                    player.Bombs = reader.ReadGChar();
                    break;

                case PlayerPropertyId.ArrowsCount:
                    player.Arrows = reader.ReadGChar();
                    break;

                case PlayerPropertyId.CurrentPower:
                    player.Hitpoints = reader.ReadGChar() / 2.0f;
                    break;

                case PlayerPropertyId.GlovePower:
                    player.GlovePower = reader.ReadGChar();
                    break;

                case PlayerPropertyId.ShieldPower:
                    player.ShieldPower = reader.ReadGChar();
                    break;

                case PlayerPropertyId.SwordPower:
                    player.SwordPower = reader.ReadGChar();
                    break;

                case PlayerPropertyId.MaxPower:
                    player.MaxPower = reader.ReadGChar();
                    break;

                case PlayerPropertyId.Status:
                    player.Status = (PlayerStatus)reader.ReadGChar();
                    break;

                default:
                    throw new NotSupportedException($"Pickup reward prop {(byte)propertyId} is not produced by confirmed LevelItem::getItemPlayerProp.");
            }
        }
    }

    private static byte NextGlovePower(LevelItemType itemType, byte current)
    {
        if (itemType == LevelItemType.Glove2)
            return 3;
        return current < 2 ? (byte)2 : current;
    }

    private static byte NextShieldPower(LevelItemType itemType, byte current)
    {
        var candidate = itemType switch
        {
            LevelItemType.LizardShield => 3,
            LevelItemType.MirrorShield => 2,
            _ => 1
        };

        return (byte)Math.Max(current, candidate);
    }

    private static byte NextSwordPower(LevelItemType itemType, byte current)
    {
        var candidate = itemType switch
        {
            LevelItemType.GoldenSword => 4,
            LevelItemType.LizardSword => 3,
            LevelItemType.BattleAxe => 2,
            _ => 1
        };

        return (byte)Math.Max(current, candidate);
    }
}
