using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.UI;

namespace Terraria.GameContent.UI.Elements;

public class UIToggleImage : UIElement
{
	private Asset<Texture2D> _onTexture;

	private Asset<Texture2D> _offTexture;

	private int _drawWidth;

	private int _drawHeight;

	private Point _onTextureOffset = Point.Zero;

	private Point _offTextureOffset = Point.Zero;

	private bool _isOn;

	public bool IsOn => _isOn;

	public UIToggleImage(Asset<Texture2D> texture, int width, int height, Point onTextureOffset, Point offTextureOffset)
	{
		_onTexture = texture;
		_offTexture = texture;
		_offTextureOffset = offTextureOffset;
		_onTextureOffset = onTextureOffset;
		_drawWidth = width;
		_drawHeight = height;
		Width.Set(width, 0f);
		Height.Set(height, 0f);
	}

	protected override void DrawSelf(SpriteBatch spriteBatch)
	{
		CalculatedStyle dimensions = GetDimensions();
		Texture2D sequence;
		Point num;
		if (_isOn)
		{
			sequence = _onTexture.Value;
			num = _onTextureOffset;
		}
		else
		{
			sequence = _offTexture.Value;
			num = _offTextureOffset;
		}
		Color color = (base.IsMouseHovering ? Color.White : Color.Silver);
		spriteBatch.Draw(sequence, new Rectangle((int)dimensions.X, (int)dimensions.Y, _drawWidth, _drawHeight), new Rectangle(num.X, num.Y, _drawWidth, _drawHeight), color);
	}

	public override void LeftClick(UIMouseEvent evt)
	{
		Toggle();
		base.LeftClick(evt);
	}

	public void SetState(bool value)
	{
		_isOn = value;
	}

	public void Toggle()
	{
		_isOn = !_isOn;
	}
}
