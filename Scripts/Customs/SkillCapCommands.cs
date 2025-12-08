using Server.Commands;
using Server.Commands.Generic;

namespace Server.Customs
{
    public static class SkillsCapCommand
    {
        public static void Initialize()
        {
            TargetCommands.Register(new SetAllSkillcapsCommand());
            TargetCommands.Register(new SetTotalSkillcapCommand());
        }
    }

    public class SetAllSkillcapsCommand : BaseCommand
    {
        public SetAllSkillcapsCommand()
        {
            AccessLevel = AccessLevel.GameMaster;
            Supports = CommandSupport.AllMobiles;
            ObjectTypes = ObjectTypes.Mobiles;
            Commands = new[] { "SetAllSkillcaps" };
            Usage = "SetAllSkillcaps [value]";
            Description =
                "Sets all skills cap values to the specified value, if not defined it will load the value from the config.";
        }

        public override void Execute(CommandEventArgs e, object obj)
        {
            var from = e.Mobile;
            var mob = (Mobile)obj;

            double value;
            if (e.Arguments.Length <= 0 || string.IsNullOrEmpty(e.Arguments[0]))
            {
                from.SendMessage("No Value given, loading defaults!");
                value = Config.Get("PlayerCaps.SkillCap", 1000) / 10;
            }
            else if (!double.TryParse(e.Arguments[0], out value))
            {
                from.SendMessage($"\"{e.Arguments[0]}\" is not a valid value! Aborted!");
                return;
            }

            foreach (var skill in mob.Skills)
                skill.Cap = value;
            from.SendMessage($"Set individual skill caps to {value} for Mobile: {mob.RawName}");

            CommandLogging.LogChangeProperty(from, mob, "EverySkill.Caps", value.ToString());
        }
    }

    public class SetTotalSkillcapCommand : BaseCommand
    {
        public SetTotalSkillcapCommand()
        {
            AccessLevel = AccessLevel.GameMaster;
            Supports = CommandSupport.AllMobiles;
            ObjectTypes = ObjectTypes.Mobiles;
            Commands = new[] { "SetTotalSkillcap" };
            Usage = "SetTotalSkillcap [value]";
            Description =
                "Sets the total skills cap value to the specified value, if not defined it will load the value from the config.";
        }

        public override void Execute(CommandEventArgs e, object obj)
        {
            var from = e.Mobile;
            var mob = (Mobile)obj;

            int value;
            if (e.Arguments.Length <= 0 || string.IsNullOrEmpty(e.Arguments[0]))
            {
                from.SendMessage("No Value given, loading defaults!");
                value = Config.Get("PlayerCaps.TotalSkillsCap", 7000);
            }
            else if (!int.TryParse(e.Arguments[0], out value))
            {
                from.SendMessage($"\"{e.Arguments[0]}\" is not a valid value! Aborted!");
                return;
            }
            mob.Skills.Cap = value;
            from.SendMessage($"Set total skill cap to {value/10}.0 for Mobile: {mob.RawName}");
            CommandLogging.LogChangeProperty(
                from,
                mob,
                "EverySkill.TotalSkillsCap",
                value.ToString()
            );
        }
    }
}
