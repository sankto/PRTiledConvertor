using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.IO;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace PRTiledConvertor {
	[Flags]
	[JsonConverter(typeof(StringEnumConverter))]
	public enum Direction : byte {
		None = 0,
		North = 1 << 0,
		South = 1 << 1,
		West = 1 << 2,
		East = 1 << 3,
		NorthWest = 1 << 4,
		NorthEast = 1 << 5,
		SouthWest = 1 << 6,
		SouthEast = 1 << 7
	};

	class Mapping {
		public const string COLLISION_SHEETID = "floors";
		public const string GROUND_LAYER = "ground";
		public const string DOODADS_LAYER = "doodads";
		public const string NPCS_LAYER = "npcs";
		public const string COLLISIONS_LAYER = "collisions";

		[JsonProperty("SpriteSheets")]
		public List<SpriteSheet> Sheets;
		[JsonProperty("SpriteSets")]
		public List<SpriteSet> Sets;

		public static Dictionary<Tuple<int, int>, Direction> CollisionTiles = new Dictionary<Tuple<int, int>, Direction>(){
			{ Tuple.Create(0, 0), Direction.North | Direction.West },
			{ Tuple.Create(1, 0), Direction.North },
			{ Tuple.Create(2, 0), Direction.North | Direction.East },
			{ Tuple.Create(3, 0), Direction.North | Direction.West | Direction.East },
			{ Tuple.Create(5, 0), Direction.North | Direction.West | Direction.East | Direction.South },

			{ Tuple.Create(0, 1), Direction.West },
			{ Tuple.Create(1, 1), Direction.None },
			{ Tuple.Create(2, 1), Direction.East },
			{ Tuple.Create(3, 1), Direction.West | Direction.East },
			{ Tuple.Create(4, 1), Direction.North | Direction.West | Direction.South },
			{ Tuple.Create(5, 1), Direction.North | Direction.South },
			{ Tuple.Create(6, 1), Direction.North | Direction.East | Direction.South },

			{ Tuple.Create(0, 2), Direction.West | Direction.South },
			{ Tuple.Create(1, 2), Direction.South },
			{ Tuple.Create(2, 2), Direction.East | Direction.South },
			{ Tuple.Create(3, 2), Direction.South | Direction.West | Direction.East },
		};

		public SpriteSet FindSpriteset(Tile tile) {
			if (tile != null)
				foreach (var set in Sets) {
					if (tile.SheetId == set.SheetId)
						if (set.TileLocations.Any((t) => t.X == tile.X && t.Y == tile.Y))
							return set;
				}
			return null;
		}

		public SpriteSheet FindSheet(Tile tile) {
			if (tile == null)
				return null;
			return Sheets.FirstOrDefault((s) => s.Id == tile.SheetId);
		}

		public static Direction GetCollisionDirection(Tile tile) {
			if (tile.SheetId == COLLISION_SHEETID) {
				var result = from c in CollisionTiles
							 where c.Key.Item1 == tile.X && c.Key.Item2 == tile.Y
							 select c.Value;
				if (result.Count() != 0)
					return result.First();
			}
			return Direction.None;
		}
	}

	class SpriteSheet {
		public string Id;
		public string File;
		public int Width;
		public int Height;
		public string Style;
		public bool MixWithOwnStyle;
		public string LinkedSheetId;
		public Dictionary<Direction, string> StyleSetByDirection;

		private string FileName = null;
		public string GetName() {
			if (FileName == null)
				FileName = Path.GetFileNameWithoutExtension(File);
			return FileName;
		}
	}

	class SpriteSet {
		public string Id;
		public string SheetId;
		public List<SpriteSetTile> TileLocations;
		public string RepresentSetId;

		public SpriteSetTile GetFromStyle(string name) {
			return TileLocations.FirstOrDefault((t) => t.Id == name);
		}
	}

	class SpriteSetTile {
		public string Id;
		public int X;
		public int Y;
	}

	class Tile {
		public string SheetId;
		public int X;
		public int Y;
	}

	class NPC {
		public string Id;
		public Tile SheetTile;
		public bool Flipped;
		public int X;
		public int Y;
	}

	class Map {
		[JsonProperty("width")]
		public int Width;
		[JsonProperty("height")]
		public int Height;
		[JsonProperty("tilewidth")]
		public int TileWidth;
		[JsonProperty("tileheight")]
		public int TileHeight;
		[JsonProperty("tilesets")]
		public List<MapTileset> Tilesets;
		[JsonProperty("layers")]
		public List<Layer> Layers;

		public Tile GetTileFromGID(int gid, Mapping mapping) {
			var set = Tilesets.First((s) => s.IsGIDWithin(gid));
			var localGID = (gid - (set.FirstGID));
			return new Tile() {
				X = localGID % (set.ImageWidth / TileWidth),
				Y = localGID / (set.ImageWidth / TileWidth),
				SheetId = mapping.Sheets.First((s) => s.GetName() == set.Name).Id
			};
		}

		public void InitTiles(Mapping mapping) {
			foreach (var layer in Layers)
				layer.InitTiles(this, mapping);
		}

		public Layer GetNPCLayer() {
			return Layers.FirstOrDefault(layer => layer.Name == Mapping.NPCS_LAYER);
		}

		public Layer GetCollisionsLayer() {
			return Layers.FirstOrDefault(layer => layer.Name == Mapping.COLLISIONS_LAYER);
		}
	}

	class Layer {
		[JsonProperty("width")]
		public int Width;
		[JsonProperty("height")]
		public int Height;
		[JsonProperty("data")]
		public List<int> Data;
		[JsonProperty("name")]
		public string Name;
		[JsonProperty("objects")]
		public List<MapObject> Objects;

		private List<Tile> tiles = null;
		public List<Tile> Tiles { get { return tiles; } }
		private List<Tile> modTiles = null;
		public List<Tile> ModTiles { get { return modTiles; } }
		private List<Direction> collisions = null;
		public List<Direction> Collisions { get { return collisions; } }
		private List<NPC> npcs = null;
		public List<NPC> NPCs { get { return npcs; } }

		public void InitTiles(Map map, Mapping mapping) {
			if (tiles == null) {
				tiles = new List<Tile>();
				modTiles = new List<Tile>();
				collisions = new List<Direction>();
				npcs = new List<NPC>();
				if (Data != null) {
					for (int i = 0; i < Data.Count; i++) {
						tiles.Add(Data[i] != 0 ? map.GetTileFromGID(Data[i], mapping) : null);
						modTiles.Add(null);
						if (Name == Mapping.COLLISIONS_LAYER) {
							collisions.Add(tiles[i] == null ? Direction.None : Mapping.GetCollisionDirection(tiles[i]));
						}
					}
				}
			}
		}

		public bool IsWithinMap(int x, int y) {
			return x >= 0 && y >= 0 && x < Width && y < Height;
		}

		public Tile GetTile(int x, int y) {
			int i = (y * Width) + x;
			return Tiles[i];
		}

		public void SetTile(int x, int y, Tile tile) {
			int i = (y * Width) + x;
			Tiles[i] = tile;
		}

		public Tile GetModTile(int x, int y) {
			int i = (y * Width) + x;
			return ModTiles[i];
		}

		public void SetModTile(int x, int y, Tile tile) {
			int i = (y * Width) + x;
			ModTiles[i] = tile;
		}
	}

	class MapObject {
		const int FLIPH_BIT = 31;
		const int FLIPV_BIT = 30;
		const int ROTATE_BIT = 29;

		[JsonProperty("gid")]
		public long GID;
		[JsonProperty("properties")]
		public ObjectProperties Properties;
		[JsonProperty("x")]
		public int X;
		[JsonProperty("y")]
		public int Y;

		public string this[string propIndex] {
			get { return Properties[propIndex]; }
		}

		public bool IsFlipped() {
			var bits = new BitArray(BitConverter.GetBytes(GID));
			return bits[FLIPH_BIT];
		}

		public int GetCleanGID() {
			var bits = new BitArray(BitConverter.GetBytes(GID));
			bits.Set(FLIPH_BIT, false);
			bits.Set(FLIPV_BIT, false);
			bits.Set(ROTATE_BIT, false);
			var newgid = new byte[32]; // that's a Int32...
			bits.CopyTo(newgid, 0);
			return (int)BitConverter.ToInt64(newgid, 0); // now it's a long.
		}
	}

	class ObjectProperties {
		[JsonExtensionData]
		private IDictionary<string, JToken> AdditionalData;

		public string this[string index] {
			get {
				return AdditionalData[index].Value<string>();
			}
		}
	}

	class MapTileset {
		[JsonProperty("firstgid")]
		public int FirstGID;
		[JsonProperty("imagewidth")]
		public int ImageWidth;
		[JsonProperty("imageheight")]
		public int ImageHeight;
		[JsonProperty("tilecount")]
		public int TileCount;
		[JsonProperty("name")]
		public string Name;

		public bool IsGIDWithin(int gid) {
			return gid >= FirstGID && gid < FirstGID + TileCount;
		}
	}
}
