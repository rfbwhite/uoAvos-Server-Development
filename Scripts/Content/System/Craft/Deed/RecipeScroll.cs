﻿using Server.Engines.Craft;
using Server.Mobiles;
using Server.Network;

namespace Server.Items
{
	public class RecipeScroll : Item
	{
		public override int LabelNumber => 1074560;  // recipe scroll

		private int m_RecipeID;

		[CommandProperty(AccessLevel.GameMaster)]
		public int RecipeID
		{
			get => m_RecipeID;
			set { m_RecipeID = value; InvalidateProperties(); }
		}

		public Recipe Recipe
		{
			get
			{
				if (Recipe.Recipes.ContainsKey(m_RecipeID))
				{
					return Recipe.Recipes[m_RecipeID];
				}

				return null;
			}
		}

		public override void GetProperties(ObjectPropertyList list)
		{
			base.GetProperties(list);

			var r = Recipe;

			if (r != null)
			{
				list.Add(1049644, r.TextDefinition.ToString()); // [~1_stuff~]
			}
		}

		public RecipeScroll(Recipe r)
			: this(r.ID)
		{
		}

		[Constructable]
		public RecipeScroll(int recipeID)
			: base(0x2831)
		{
			m_RecipeID = recipeID;
		}

		public RecipeScroll(Serial serial)
			: base(serial)
		{
		}

		public override void OnDoubleClick(Mobile from)
		{
			if (!from.InRange(GetWorldLocation(), 2))
			{
				from.LocalOverheadMessage(MessageType.Regular, 0x3B2, 1019045); // I can't reach that.
				return;
			}

			var r = Recipe;

			if (r != null && from is PlayerMobile)
			{
				var pm = from as PlayerMobile;

				if (!pm.HasRecipe(r))
				{
					var allRequiredSkills = true;
					var chance = r.CraftItem.GetSuccessChance(from, null, r.CraftSystem, false, ref allRequiredSkills);

					if (allRequiredSkills && chance >= 0.0)
					{
						pm.SendLocalizedMessage(1073451, r.TextDefinition.ToString()); // You have learned a new recipe: ~1_RECIPE~
						pm.AcquireRecipe(r);
						Delete();
					}
					else
					{
						pm.SendLocalizedMessage(1044153); // You don't have the required skills to attempt this item.
					}
				}
				else
				{
					pm.SendLocalizedMessage(1073427); // You already know this recipe.
				}

			}
		}

		public override void Serialize(GenericWriter writer)
		{
			base.Serialize(writer);

			writer.Write(0); // version

			writer.Write(m_RecipeID);
		}

		public override void Deserialize(GenericReader reader)
		{
			base.Deserialize(reader);

			var version = reader.ReadInt();

			switch (version)
			{
				case 0:
					{
						m_RecipeID = reader.ReadInt();

						break;
					}
			}
		}
	}
}