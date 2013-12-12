/*!
 * X-UniTMX: A tiled map editor file importer for Unity3d
 * https://bitbucket.org/Chaoseiro/x-unitmx
 * 
 * Copyright 2013 Guilherme "Chaoseiro" Maia
 * Released under the MIT license
 * Check LICENSE.MIT for more details.
 */

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.IO;
using UnityEngine;

namespace X_UniTMX
{
	/// <summary>
	/// A Container for a Tile Set properties and its Tiles.
	/// </summary>
	public class TileSet
	{
		public int FirstId;
		public string Name;
		public int TileWidth;
		public int TileHeight;
		public int Spacing;
		public int Margin;
		public string Image;
		public Color? ColorKey;
		public Texture2D Texture;
		public Dictionary<int, Tile> Tiles = new Dictionary<int, Tile>();
		public Dictionary<int, PropertyCollection> TileProperties = new Dictionary<int, PropertyCollection>();

		public TileSet(XmlNode node, string mapPath)
		{
			this.FirstId = int.Parse(node.Attributes["firstgid"].Value, CultureInfo.InvariantCulture);
			this.Name = node.Attributes["name"].Value;
			this.TileWidth = int.Parse(node.Attributes["tilewidth"].Value, CultureInfo.InvariantCulture);
			this.TileHeight = int.Parse(node.Attributes["tileheight"].Value, CultureInfo.InvariantCulture);

			if (node.Attributes["spacing"] != null)
			{
				this.Spacing = int.Parse(node.Attributes["spacing"].Value, CultureInfo.InvariantCulture);
			}

			if (node.Attributes["margin"] != null)
			{
				this.Margin = int.Parse(node.Attributes["margin"].Value, CultureInfo.InvariantCulture);
			}

			XmlNode imageNode = node["image"];
			this.Image = imageNode.Attributes["source"].Value;

			// if the image is in any director up from us, just take the filename
			if (this.Image.StartsWith(".."))
				this.Image = Path.GetFileName(this.Image);

			//this.Image = mapPath + "/" + this.Image;
			//Debug.Log(this.Image);

			if (imageNode.Attributes["trans"] != null)
			{
				string color = imageNode.Attributes["trans"].Value;
				string r = color.Substring(0, 2);
				string g = color.Substring(2, 2);
				string b = color.Substring(4, 2);
				this.ColorKey = new Color((byte)Convert.ToInt32(r, 16), (byte)Convert.ToInt32(g, 16), (byte)Convert.ToInt32(b, 16));
			}
			foreach (XmlNode tileProperty in node.SelectNodes("tile"))
			{
				int id = this.FirstId + int.Parse(tileProperty.Attributes["id"].Value, CultureInfo.InvariantCulture);
				//List<Property> properties = new List<Property>();
				PropertyCollection properties = new PropertyCollection();

				XmlNode propertiesNode = tileProperty["properties"];
				if (propertiesNode != null)
				{
					properties = new PropertyCollection(propertiesNode);//Property.ReadProperties(propertiesNode);
				}

				this.TileProperties.Add(id, properties);
			}

			// Build tiles from this tileset
			this.Texture = (Texture2D)Resources.Load(mapPath + Path.GetFileNameWithoutExtension(this.Image), typeof(Texture2D));
			
			//int imageWidth = this.Texture.width - Margin * 2;
			//int imageHeight = this.Texture.height - Margin * 2;

			// figure out how many frames fit on the X axis
			int frameCountX = -(2 * Margin - Spacing - this.Texture.width) / (TileWidth + Spacing);
			/*while (frameCountX * TileWidth < imageWidth)
			{
				frameCountX++;
				imageWidth -= Spacing;
			}*/
			//frameCountX--;

			// figure out how many frames fit on the Y axis
			int frameCountY = -(2 * Margin - Spacing - this.Texture.height) / (TileHeight + Spacing);
			/*while (frameCountY * TileHeight < imageHeight)
			{
				frameCountY++;
				imageHeight -= Spacing;
			}*/
			//frameCountY--;

			// make our tiles. tiles are numbered by row, left to right.
			for (int y = 0; y < frameCountY; y++)
			{
				for (int x = 0; x < frameCountX; x++)
				{
					//Tile tile = new Tile();

					// calculate the source rectangle
					int rx = Margin + x * (TileWidth + Spacing);
					int ry = Margin + y * (TileHeight + Spacing);
					Rect Source = new Rect(rx, ry, TileWidth, TileHeight);

					// get any properties from the tile set
					int index = FirstId + (y * frameCountX + x);
					PropertyCollection Properties = new PropertyCollection();
					if (TileProperties.ContainsKey(index))
					{
						Properties = TileProperties[index];
					}
					
					// save the tile
					Tiles.Add(index, new Tile(this, Source, index, Properties));
				}
			}
		}
		
	}

}