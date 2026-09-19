using System.Drawing;
using System.Windows.Forms;
using Vortice.Mathematics;

class GameMain : G2AppBase
{
    public override System.Drawing.Size ScreenSize => GameGlobal.ScreenSize;
    public override string GameName => GameGlobal.GameName;

    private G2Texture? _bgTexture;
    private G2Texture? _logoTexture;
    private G2Texture? _startButtonTexture;

    private PointF _logoPosition;
    private PointF _startButtonPosition;

    protected override void Initialize()
    {
        _bgTexture = new G2Texture("main.png");
        _logoTexture = new G2Texture("logo.png");
        _startButtonTexture = new G2Texture("start.png");

        // 로고: 화면 상단 중앙
        _logoPosition = new PointF(
            (ScreenSize.Width - _logoTexture.Width) / 2f,
            80f);

        // 시작 버튼: 화면 하단 중앙
        _startButtonPosition = new PointF(
            (ScreenSize.Width - _startButtonTexture.Width) / 2f,
            ScreenSize.Height - _startButtonTexture.Height - 80f);
    }

    protected override void Update()
    {
        var buttonRect = new RectangleF(
            _startButtonPosition.X, _startButtonPosition.Y,
            _startButtonTexture!.Width, _startButtonTexture.Height);

        if (Input.IsButtonDown(MouseButtons.Left) && buttonRect.Contains(Input.MousePosition))
        {
            // TODO: 8강 대진표 화면으로 전환 (다음 단계에서 구현)
        }
    }

    protected override void Render()
    {
        // 배경: 화면 전체(960x640)에 꽉 차게 스트레치
        var screenRect = new Rect(0f, 0f, ScreenSize.Width, ScreenSize.Height);
        var bgSourceRect = new Rect(0f, 0f, _bgTexture!.Width, _bgTexture.Height);
        _bgTexture.Draw(screenRect, bgSourceRect);

        // 로고 & 버튼: 원본 크기 그대로
        _logoTexture!.Draw(_logoPosition.X, _logoPosition.Y);
        _startButtonTexture!.Draw(_startButtonPosition.X, _startButtonPosition.Y);
    }

    public override void Dispose()
    {
        base.Dispose();
        _bgTexture?.Dispose();
        _logoTexture?.Dispose();
        _startButtonTexture?.Dispose();
    }
}

