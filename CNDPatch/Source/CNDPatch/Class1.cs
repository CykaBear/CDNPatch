using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;
using RimWorld;
using UnityEngine;

namespace CNDPatch
{

	public class CNDPatcher
	{
		
		[StaticConstructorOnStartup]
		internal static class HarmonyInit
		{
			static HarmonyInit() // Patch rimworld assembly on startup
			{
				new Harmony("CDN.Patch").PatchAll(Assembly.GetExecutingAssembly());
			}
		}

		[HarmonyPatch]
		private class HealthPatcher
		{
			static MethodBase TargetMethod()
			{
				// Targetting method from Colonists Never Die that handles part health
				var type = AccessTools.FirstInner(typeof(ColonistsNeverDie.ColonistsNeverDie), t => t.Name.Contains("GetPartHealthPatch"));
				return AccessTools.FirstMethod(type, method => method.Name.Contains("Postfix"));
			}

			static bool Prefix()
			{
				// Stop their code from running
				return true;

			}
		}

		[HarmonyPatch]
		private class DeathPatcher
		{
			static MethodBase TargetMethod()
			{
				// Target method from Colonists Never Die that handles the death prefix
				var type = AccessTools.FirstInner(typeof(ColonistsNeverDie.ColonistsNeverDie), t => t.Name.Contains("TryGetAttackVerbPatch"));
				return AccessTools.FirstMethod(type, method => method.Name.Contains("Prefix"));
			}

			static void Postfix(ref bool __result, Pawn __instance)
			{
				// Cancel the results of their prefix
				__result = true;


			}
		}
		[HarmonyPatch]
		private class CNDBodyPartPatcher
		{
			static MethodBase TargetMethod()
			{
				var type = AccessTools.FirstInner(typeof(ColonistsNeverDie.ColonistsNeverDie), t => t.Name.Contains("GetPartHealthPatch"));
				return AccessTools.FirstMethod(type, method => method.Name.Contains("Postfix"));
			}

			static bool Prefix()
			{

				return false;
			}
		}



		[HarmonyPatch(typeof(Pawn), "Kill")] // Run our own patch on pawn deaths
		private class NewPatcher
		{

			static bool Prefix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
			{
				if (__instance.IsColonist && !__instance.Dead)
				{

					if (dinfo != null)
					{
						String usefulDamageInfo = dinfo.ToString().Split(',')[0]; // Check if they were damaged by an execution cut to allow death
						String damageDef = usefulDamageInfo.Split('=')[1];
						if (damageDef == "ExecutionCut")
						{
							return true;
						}
					}
					if (exactCulprit != null)
					{
						if (exactCulprit.CauseDeathNow())
						{
							if (exactCulprit.def.isBad) // Blanket check for most bad hediffs
							{
								if (exactCulprit.def.defName == "BloodLoss")
								{
									return false;
								}
								if (exactCulprit.def.defName == "Heatstroke")
								{
									return false;
								}
								if (exactCulprit.def.defName == "Hypothermia")
								{
									return false;
								}
								return true;
							}
						}
					}
					int lungCount = 0;
					int kidneyCount = 0;

					foreach (Hediff_MissingPart v in __instance.health.hediffSet.GetMissingPartsCommonAncestors())
					{
						if (v.Part.def.defName == "Neck")
						{
							return true; // Kill if neck is missing
						}
						if (v.Part.IsInGroup(BodyPartGroupDefOf.Torso))
						{
							if (v.Part.def.defName == "Lung")
							{
								lungCount += 1; // Count lungs missing
							}


						}
						if (v.Part.IsInGroup(BodyPartGroupDefOf.Torso))
						{

							if (v.Part.def.defName == "Kidney")
							{
								kidneyCount += 1; // Count kidneys missing
							}


						}
						if (v.Part.IsInGroup(BodyPartGroupDefOf.Torso))
						{

							if (kidneyCount == 2 || lungCount == 2)
							{
								return true; // Kill if two lungs or kidneys are missing
							}
							if (v.Part.def.defName == "Heart")
							{
								return true; // Kill if heart is missing
							}
							if (v.Part.def.defName == "Liver")
							{
								return true; // Kill if liver is missing
							}
							if (v.Part.def.defName == "Stomach")
							{
								return true; // Kill if stomach is missing
							}
						}
						if (v.Part.IsInGroup(BodyPartGroupDefOf.UpperHead))
						{
							if (v.Part.def.defName == "Brain" || v.Part.def.defName == "Head" || v.Part.def.defName == "Skull")
							{
								return true; // Kill if Brain, head or skull is missing
							}
						}
					}

					return false;
				}
				return true;
			}

		}
		[RimWorld.DefOf]
		public static class DefOf
		{
			public static BodyPartGroupDef Waist;

			public static BodyPartGroupDef Neck;

			public static BodyPartDef Ear;

			static DefOf()
			{
				DefOfHelper.EnsureInitializedInCtor(typeof(DefOf));
			}
		}
		[HarmonyPatch(typeof(HediffSet), "GetPartHealth")]
		private class GetPartHealthPatch
		{
			
			private static readonly List<BodyPartGroupDef> vitalgroups = new List<BodyPartGroupDef>
		{
			DefOf.Neck,
			BodyPartGroupDefOf.Torso,
			DefOf.Waist,
			BodyPartGroupDefOf.UpperHead
		};

			public static void Postfix(ref float __result, BodyPartRecord part, HediffSet __instance)
			{


				if (!__instance.pawn.IsColonist)
				{
					return;
				}


				foreach (BodyPartGroupDef vitalgroup in vitalgroups)
				{
					
					if (part.IsInGroup(vitalgroup))
					{
						if (part.IsInGroup(vitalgroup) && !__instance.PartIsMissing(part) && !__instance.HasHediff(HediffDefOf.SurgicalCut))
						{
							float num = Mathf.RoundToInt(Mathf.Max(__result, 1f));
							__result = num;
							return;
						}
					}
				}
			}
		}
	}

	
}
