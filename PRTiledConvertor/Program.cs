using System;
using System.Linq;
using Newtonsoft.Json;
using System.IO;

namespace PRTiledConvertor {
	class Program {
		public static Direction GetSimilarTiles(int posx, int posy, Layer layer, Mapping mapping) {
			var tile = layer.GetTile(posx, posy);
			var result = Direction.None;

			if (tile == null) // null = empty tile
				return result;

			var spriteSet = mapping.FindSpriteset(tile);
			var sheet = mapping.FindSheet(tile);

			Tile t = null;
			SpriteSet ts = null;
			SpriteSheet tsh = null;

			bool west = !layer.IsWithinMap(posx - 1, posy); // if west is outside
			if (!west) {
				t = layer.GetTile(posx - 1, posy);
				ts = mapping.FindSpriteset(t);
				tsh = mapping.FindSheet(t);
				west = spriteSet == null && ts == null; // if west is empty AND we look for empty tile
				west = west ? west : ts != null && spriteSet != null && tsh.Style == sheet.Style && // if west match style AND (if can mix with other spritesets OR west is the same spriteset)
						(tsh.MixWithOwnStyle || (!tsh.MixWithOwnStyle && ts == spriteSet));
			}

			bool east = !layer.IsWithinMap(posx + 1, posy);
			if (!east) {
				t = layer.GetTile(posx + 1, posy);
				ts = mapping.FindSpriteset(t);
				tsh = mapping.FindSheet(t);
				east = spriteSet == null && ts == null;
				east = east ? east : ts != null && spriteSet != null && tsh.Style == sheet.Style &&
						(tsh.MixWithOwnStyle || (!tsh.MixWithOwnStyle && ts == spriteSet));
			}

			bool north = !layer.IsWithinMap(posx, posy - 1);
			if (!north) {
				t = layer.GetTile(posx, posy - 1);
				ts = mapping.FindSpriteset(t);
				tsh = mapping.FindSheet(t);
				north = spriteSet == null && ts == null;
				north = north ? north : ts != null && spriteSet != null && tsh.Style == sheet.Style &&
						(tsh.MixWithOwnStyle || (!tsh.MixWithOwnStyle && ts == spriteSet));
			}

			bool south = !layer.IsWithinMap(posx, posy + 1);
			if (!south) {
				t = layer.GetTile(posx, posy + 1);
				ts = mapping.FindSpriteset(t);
				tsh = mapping.FindSheet(t);
				south = spriteSet == null && ts == null;
				south = south ? south : ts != null && spriteSet != null && tsh.Style == sheet.Style &&
						(tsh.MixWithOwnStyle || (!tsh.MixWithOwnStyle && ts == spriteSet));
			}

			if (west)
				result |= Direction.West;
			if (east)
				result |= Direction.East;
			if (north)
				result |= Direction.North;
			if (south)
				result |= Direction.South;

			return result;
		}

		public static void ProcessStyleable(Map map, Mapping mapping, Layer layer) {
			// we go through each tile, left to right, top to bottom
			for (int y = 0; y < map.Height; y++)
				for (int x = 0; x < map.Width; x++) {
					// get the tile that need to be observed
					var tile = layer.GetTile(x, y);
					// if this is left to True, we simply don't change the tile at all at the end
					bool noModification = true;
					// if the tile is empty
					if (tile != null) {
						// we get the sheet associated with the tile, from the 'mapping' source of all sheets.
						// it's unlikely to be null.
						var sheet = mapping.Sheets.FirstOrDefault((s) => s.Id == tile.SheetId);
						// then we get the spriteset, if any, of the tile. Null if none associated.
						var set = mapping.FindSpriteset(tile);
						var initialStyle = set?.TileLocations.FirstOrDefault((t) => t.X == tile.X && t.Y == tile.Y).Id;
						// if the spriteset is nonull and there is a style direction pattern associated with
						// the sheet, and if the tile is the one being represented (ie. the tile == used in Tiled),
						// the tile is elligible for transformation.
						if (set != null && sheet.StyleSetByDirection != null && set.RepresentSetId == initialStyle) {
							noModification = false;
							// we get the directions of which the tile have compatible tiles to it,
							// in the form a flagged enum.
							var dirs = GetSimilarTiles(x, y, layer, mapping);
							// we get the 'style' of the new tile, via the sheet pattern.
							// style include IDs such as 'br' (bottom right), 'full', 'tlr' (top-left-right), etc.
							// they can be a bit confusing.
							var style = sheet.StyleSetByDirection[dirs];
							// then we get the tile within the spriteset that is associated with that style id.
							var setTile = set.GetFromStyle(style);
							// The tile is now set to the new tile informations.
							layer.SetModTile(x, y, new Tile() {
								SheetId = tile.SheetId,
								X = setTile.X,
								Y = setTile.Y
							});
						}
					}
					if (noModification)
						layer.SetModTile(x, y, tile);
				}
		}

		public static void ProcessNPCs(Map map, Mapping mapping, Layer layer) {
			foreach (var obj in layer.Objects) {
				layer.NPCs.Add(new NPC() {
					Id = obj["id"],
					SheetTile = map.GetTileFromGID(obj.GetCleanGID(), mapping),
					Flipped = obj.IsFlipped(),
					X = obj.X / map.TileWidth,
					Y = obj.Y / map.TileHeight
				});
			}
		}

		public static void ProcessMap(Map map, Mapping mapping) {
			// we go through each layers of the map
			foreach (var layer in map.Layers) {
				if (layer.Name == Mapping.GROUND_LAYER || layer.Name == Mapping.DOODADS_LAYER) {
					ProcessStyleable(map, mapping, layer);
				} else if (layer.Name == Mapping.NPCS_LAYER) {
					ProcessNPCs(map, mapping, layer);
				}
				// collisions are handled in classes Mapping and Layer
			}
		}

		static void Main(string[] args) {
			if (File.Exists(@"load.txt")) {
				var paths = File.ReadAllLines(@"load.txt");
				var mapped = JsonConvert.DeserializeObject<Mapping>(File.ReadAllText(paths[0]));
				var map = JsonConvert.DeserializeObject<Map>(File.ReadAllText(paths[1]));

				map.InitTiles(mapped);
				ProcessMap(map, mapped);

				File.WriteAllText(Path.GetDirectoryName(paths[1]) + @"\" + Path.GetFileNameWithoutExtension(paths[1]) + ".prmap", JsonConvert.SerializeObject(new {
					Width = map.Width,
					Height = map.Height,
					TileWidth = map.TileWidth,
					TileHeight = map.TileHeight,
					Layers = from layer in map.Layers
							 where layer.Name != Mapping.NPCS_LAYER &&
								   layer.Name != Mapping.COLLISIONS_LAYER
							 select new {
							 	 Name = layer.Name,
								 Tiles = from tile in layer.ModTiles
										 select tile == null ? null : new {
											 SId = tile.SheetId,
											 SX = tile.X,
											 SY = tile.Y
										 }
							 },
					NPCs = map.GetNPCLayer()?.NPCs,
					Collisions = map.GetCollisionsLayer()?.Collisions
				}, Formatting.Indented, new JsonSerializerSettings() { NullValueHandling = NullValueHandling.Include }));
			}
			Console.WriteLine("--------------------------------");
			Console.WriteLine("Press a key to exit.");
			Console.ReadKey();
		}
	}
}
