namespace SolarEclipse.Mods
{
    /// <summary>SandScript sources of the bundled example mods and the editor templates (no Unity dependency).</summary>
    public static class AdvancedScripts
    {
        public const string AngryChef =
@"# Angry Chef: chases anyone who comes near with his cleaver.
on spawn:
  say ""Who touched my kitchen?!""
  set speed 4.5
  set angry = 0

on see player:
  say ""YOU! Out of my kitchen!""
  play_sound yell
  chase player

every 1:
  if has_target and distance target < 2:
    attack
  if angry > 3:
    say ""RAAAH!""
    set speed 5.5

on hurt:
  set angry = angry + 1
  if chance 50:
    say ""That's it, you're on the menu!""
  target attacker
  chase attacker

on lose target:
  say ""Cowards, all of you.""
  wander

on death:
  say ""My... souffle...""
";

        public const string Coward =
@"# Coward: runs from everyone and begs for mercy.
on spawn:
  set speed 5
  set begs = 0
  wander

on see player:
  say ""Please don't hurt me!""
  flee player

on near player 4:
  crouch 2
  set begs = begs + 1
  if begs < 3:
    say ""I have a family!""
  else:
    say ""Take my wallet! Just go!""
  wait 2
  flee player

on hurt:
  play_sound scream
  if health < 40:
    say ""Mercy! MERCY!""
    ragdoll 2
  else:
    flee attacker

every 6:
  if not has_target:
    wander
";

        public const string LightningPistol =
@"# Lightning Pistol: every shot crackles, every hit zaps.
on fire:
  effect sparks
  sound zap
  if chance 20:
    spawn_projectile spark

on hit:
  effect sparks
  knockback 14
  damage_target 6
  if chance 15:
    ignite 2
";

        public const string NpcTemplate =
@"on spawn:
  say ""I'm back""
  set speed 4

on see player:
  if health < 50:
    flee
  else:
    chase player
    attack

every 2:
  if distance player < 3:
    punch

on hurt:
  say ""Ow!""

on death:
  explode 3
";

        public const string WeaponTemplate =
@"on fire:
  effect smoke

on hit:
  effect blood
  knockback 5
";
    }
}
