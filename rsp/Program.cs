using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using NAudio.Wave;

// ===================== 프로그램 시작점 =====================
internal class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new GameForm());
    }
}

// ===================== 게임 규칙 =====================
/// <summary>묵(바위) / 찌(가위) / 빠(보)</summary>
public enum Hand { Rock = 0, Scissors = 1, Paper = 2 }

public enum Side { None, Player, Opponent }

public enum TurnOutcome
{
    Redraw,        // 1라운드에서 비김 -> 다시
    AttackerSet,   // 1라운드 종료: 이긴 쪽이 공격권을 가짐
    AttackKept,    // 2라운드~: 공격권 보유자가 이김 -> 유지
    AttackChanged, // 2라운드~: 공격권 보유자가 짐 -> 이전
    GameOver       // 2라운드~: 비김 -> 공격권 보유자 승리
}

public class MukjjippaGame
{
    public int Round { get; private set; } = 1;
    public Side Attacker { get; private set; } = Side.None;
    public bool IsFinished { get; private set; }
    public Side Winner { get; private set; } = Side.None;

    private static readonly Random _rand = new Random();

    /// <summary>a가 b를 이기면 1, 지면 -1, 비기면 0 (묵 > 찌 > 빠 > 묵)</summary>
    public static int Compare(Hand a, Hand b)
    {
        if (a == b) return 0;
        bool aWins =
            (a == Hand.Rock && b == Hand.Scissors) ||
            (a == Hand.Scissors && b == Hand.Paper) ||
            (a == Hand.Paper && b == Hand.Rock);
        return aWins ? 1 : -1;
    }

    public TurnOutcome Play(Hand player, Hand opponent)
    {
        if (IsFinished)
            throw new InvalidOperationException("이미 끝난 게임입니다. Reset()을 호출하세요.");

        int result = Compare(player, opponent);

        // 1라운드: 일반 가위바위보
        if (Round == 1)
        {
            if (result == 0) return TurnOutcome.Redraw;

            Attacker = (result > 0) ? Side.Player : Side.Opponent;
            Round++;
            return TurnOutcome.AttackerSet;
        }

        // 2라운드 이후
        Round++;

        if (result == 0)
        {
            Winner = Attacker;
            IsFinished = true;
            return TurnOutcome.GameOver;
        }

        Side roundWinner = (result > 0) ? Side.Player : Side.Opponent;

        if (roundWinner == Attacker)
            return TurnOutcome.AttackKept;

        Attacker = roundWinner;
        return TurnOutcome.AttackChanged;
    }

    public void Reset()
    {
        Round = 1;
        Attacker = Side.None;
        Winner = Side.None;
        IsFinished = false;
    }

    /// <summary>임시 상대: 완전 랜덤</summary>
    public static Hand RandomHand()
    {
        return (Hand)_rand.Next(3);
    }
}

// ===================== 깜빡임 방지용 패널 =====================
internal class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        DoubleBuffered = true;
    }
}

// ===================== 게임 화면 =====================
internal class GameForm : Form
{
    // ★ 이미지 폴더/파일 이름 (솔루션 탐색기에 보이는 이름과 똑같이)
    private const string ImageFolder = "images";
    private const string FileRock = "rock.png";
    private const string FileScissors = "scissors.png";
    private const string FilePaper = "paper.png";
    private const string FileStadium = "stadium.png";
    private const string FileBlue = "blue.png";
    private const string FileRed = "red.png";
    private const string FileLogo = "logo.png";
    private const string FileMain = "main.png";
    private const string FileStart = "start.png";

    private readonly MukjjippaGame game = new MukjjippaGame();
    private readonly Dictionary<Hand, Image> handImages = new Dictionary<Hand, Image>();

    private Panel pnlStart, pnlGame;
    private PictureBox picPlayerHand, picEnemyHand;
    private Label lblInfo, lblResult, lblBlue, lblRed;

    // 배경음악
    private WaveOutEvent bgmDevice;
    private AudioFileReader bgmReader;
    private bool closing;

    public GameForm()
    {
        Text = "묵찌빠";
        ClientSize = new Size(900, 560);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;

        handImages[Hand.Rock] = LoadImage(FileRock);
        handImages[Hand.Scissors] = LoadImage(FileScissors);
        handImages[Hand.Paper] = LoadImage(FilePaper);

        BuildStartScreen();
        BuildGameScreen();

        Controls.Add(pnlStart);
        Controls.Add(pnlGame);
        ShowStartScreen();
        PlayBgm();
    }

    // ---------- 파일 경로 찾기 ----------
    // 실행 폴더에서 위쪽 폴더로 올라가며 파일을 찾는다 (없으면 null)
    private static string FindPath(string folder, string fileName)
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;

        for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            string path = Path.Combine(dir, folder, fileName);
            if (File.Exists(path)) return path;
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }
        return null;
    }

    // ---------- 배경음악 ----------
    private void PlayBgm()
    {
        string path = FindPath("sounds", "bgm.mp3");
        if (path == null) return; // 파일이 없으면 소리 없이 진행

        bgmReader = new AudioFileReader(path);
        bgmDevice = new WaveOutEvent();
        bgmDevice.Init(bgmReader);
        bgmDevice.Volume = 0.5f; // 소리 크기 0.0 ~ 1.0

        // 끝나면 처음으로 되돌려 반복 재생
        bgmDevice.PlaybackStopped += (s, e) =>
        {
            if (closing) return;
            bgmReader.Position = 0;
            bgmDevice.Play();
        };
        bgmDevice.Play();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        closing = true;
        bgmDevice?.Dispose();
        bgmReader?.Dispose();
        base.OnFormClosing(e);
    }

    // ---------- 이미지 불러오기 ----------
    // 실행 폴더(bin\Debug\...)에서 image 폴더를 찾고,
    // 없으면 위쪽 폴더(프로젝트 폴더)로 올라가며 찾는다.
    private static Image LoadImage(string fileName)
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;

        for (int i = 0; i < 6 && !string.IsNullOrEmpty(dir); i++)
        {
            string path = Path.Combine(dir, ImageFolder, fileName);
            if (File.Exists(path))
            {
                using (Image temp = Image.FromFile(path))
                {
                    return new Bitmap(temp); // 파일 잠금 방지용 복사본
                }
            }
            dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
        }

        throw new FileNotFoundException(
            "이미지를 찾을 수 없습니다: " + ImageFolder + "\\" + fileName);
    }

    // ---------- 시작 화면 ----------
    private void BuildStartScreen()
    {
        pnlStart = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackgroundImage = LoadImage(FileMain),
            BackgroundImageLayout = ImageLayout.Stretch
        };

        // 게임 제목 로고
        pnlStart.Controls.Add(MakeImageBox(LoadImage(FileLogo), 50, 30, 800, 260));

        // 시작 버튼
        PictureBox btnStart = MakeImageBox(LoadImage(FileStart), 290, 320, 320, 150);
        btnStart.Cursor = Cursors.Hand;
        btnStart.Click += (s, e) => StartGame();
        pnlStart.Controls.Add(btnStart);
    }

    // ---------- 게임 화면 ----------
    private void BuildGameScreen()
    {
        pnlGame = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            BackgroundImage = LoadImage(FileStadium),
            BackgroundImageLayout = ImageLayout.Stretch
        };

        // 상단 안내 문구
        lblInfo = MakeLabel(0, 15, 900, 40, 20, FontStyle.Bold);
        pnlGame.Controls.Add(lblInfo);

        // 블루팀(나) / 레드팀(상대)
        pnlGame.Controls.Add(MakeImageBox(LoadImage(FileBlue), 40, 100, 180, 240));
        pnlGame.Controls.Add(MakeImageBox(LoadImage(FileRed), 680, 100, 180, 240));
        lblBlue = MakeLabel(40, 345, 180, 30, 14, FontStyle.Bold);
        lblRed = MakeLabel(680, 345, 180, 30, 14, FontStyle.Bold);
        lblBlue.Text = "블루팀 (나)";
        lblRed.Text = "레드팀 (상대)";
        pnlGame.Controls.Add(lblBlue);
        pnlGame.Controls.Add(lblRed);

        // 가운데: 각자 낸 패
        picPlayerHand = MakeImageBox(null, 260, 130, 180, 180);
        picEnemyHand = MakeImageBox(null, 460, 130, 180, 180);
        pnlGame.Controls.Add(picPlayerHand);
        pnlGame.Controls.Add(picEnemyHand);

        // 결과 문구
        lblResult = MakeLabel(0, 330, 900, 40, 18, FontStyle.Bold);
        pnlGame.Controls.Add(lblResult);

        // 아래: 가위 / 바위 / 보 선택 버튼
        AddPickButton(Hand.Scissors, 235);
        AddPickButton(Hand.Rock, 385);
        AddPickButton(Hand.Paper, 535);
    }

    // ---------- UI 도우미 ----------
    private static Label MakeLabel(int x, int y, int w, int h, float size, FontStyle style)
    {
        return new Label
        {
            Left = x,
            Top = y,
            Width = w,
            Height = h,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("맑은 고딕", size, style),
            ForeColor = Color.White,
            BackColor = Color.Transparent
        };
    }

    private static PictureBox MakeImageBox(Image img, int x, int y, int w, int h)
    {
        return new PictureBox
        {
            Left = x,
            Top = y,
            Width = w,
            Height = h,
            Image = img,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
    }

    private void AddPickButton(Hand hand, int x)
    {
        PictureBox pic = MakeImageBox(handImages[hand], x, 395, 130, 130);
        pic.Cursor = Cursors.Hand;
        pic.Click += (s, e) => OnPick(hand);
        pnlGame.Controls.Add(pic);
    }

    // ---------- 화면 전환 ----------
    private void ShowStartScreen()
    {
        pnlGame.Visible = false;
        pnlStart.Visible = true;
    }

    private void StartGame()
    {
        game.Reset();
        picPlayerHand.Image = null;
        picEnemyHand.Image = null;
        lblResult.Text = "";
        UpdateInfo();

        pnlStart.Visible = false;
        pnlGame.Visible = true;
    }

    // ---------- 게임 진행 ----------
    private void OnPick(Hand mine)
    {
        if (game.IsFinished) return;

        Hand enemy = MukjjippaGame.RandomHand();
        picPlayerHand.Image = handImages[mine];
        picEnemyHand.Image = handImages[enemy];

        TurnOutcome outcome = game.Play(mine, enemy);

        switch (outcome)
        {
            case TurnOutcome.Redraw:
                lblResult.Text = "비겼습니다! 다시!";
                break;
            case TurnOutcome.AttackerSet:
                lblResult.Text = SideName(game.Attacker) + " 공격권 획득!";
                break;
            case TurnOutcome.AttackKept:
                lblResult.Text = SideName(game.Attacker) + " 공격 유지!";
                break;
            case TurnOutcome.AttackChanged:
                lblResult.Text = "공격권이 " + SideName(game.Attacker) + "으로 넘어갔습니다!";
                break;
            case TurnOutcome.GameOver:
                lblResult.Text = SideName(game.Winner) + " 승리!";
                break;
        }

        UpdateInfo();
        Refresh();

        if (outcome == TurnOutcome.GameOver)
        {
            MessageBox.Show(SideName(game.Winner) + " 승리!\n시작 화면으로 돌아갑니다.", "게임 종료");
            ShowStartScreen();
        }
    }

    private void UpdateInfo()
    {
        if (game.Round == 1)
            lblInfo.Text = "1라운드 - 가위바위보로 공격권을 잡으세요!";
        else
            lblInfo.Text = game.Round + "라운드 - 공격권: " + SideName(game.Attacker);

        // 공격권을 가진 팀 이름을 노란색으로 강조
        lblBlue.ForeColor = (game.Attacker == Side.Player) ? Color.Yellow : Color.White;
        lblRed.ForeColor = (game.Attacker == Side.Opponent) ? Color.Yellow : Color.White;
    }

    private static string SideName(Side side)
    {
        switch (side)
        {
            case Side.Player: return "블루팀";
            case Side.Opponent: return "레드팀";
            default: return "-";
        }
    }
}