using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace XP3.Forms
{
    public partial class AppBarVisualizer : Form
    {
        private const int AlturaAppBar = 110;
        private const int AlturaMinimaAppBar = 70;
        private const int AlturaMaximaAppBar = 400;
        private const int FaixaBordaSuperior = 6;

        private bool _registrado;
        private bool _reposicionandoAppBar;
        private IntPtr _hWnd;
        private int _callbackMessage;
        private AppBarAPI.APPBARDATA _appBarData;
        private bool _redimensionando;
        private Point _posicaoInicialMouse;
        private int _alturaInicialAppBar;

        public AppBarVisualizer()
        {
            InitializeComponent();
            pnlResize.Height = FaixaBordaSuperior;

            btnFechar.BringToFront();

            this.DoubleClick += AoDuploClique;
            panelVisualizerHost.DoubleClick += AoDuploClique;
            pnlResize.DoubleClick += AoDuploClique;
        }

        // Duplo clique em área livre da AppBar abre a Visualização Full.
        private void AoDuploClique(object sender, EventArgs e)
        {
            AbrirFullSolicitado?.Invoke(this, EventArgs.Empty);
        }

        // Avisa a janela principal que a AppBar vai fechar (devolve o visualizador).
        public event EventHandler AntesDeFechar;

        // Avisa a janela principal que a Visualização Full foi solicitada.
        public event EventHandler AbrirFullSolicitado;

        // Painel que hospeda o visualizador dentro da AppBar.
        public Panel VisualizerHost
        {
            get { return panelVisualizerHost; }
        }

        // Etapa 6: titulo e progresso removidos da AppBar; o titulo agora e
        // desenhado pelo proprio SpectrumControl (propriedade TituloMusica).

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            _hWnd = Handle;
            _callbackMessage = (int)AppBarAPI.RegisterWindowMessage("AppBarMessage");
            RegistrarAppBar();
        }

        // ABM_NEW -> ABM_QUERYPOS -> ABM_SETPOS, fixado em ABE_BOTTOM.
        private void RegistrarAppBar()
        {
            if (_registrado)
                return;

            Rectangle area = Screen.PrimaryScreen.WorkingArea;

            _appBarData = new AppBarAPI.APPBARDATA();
            _appBarData.cbSize = Marshal.SizeOf(typeof(AppBarAPI.APPBARDATA));
            _appBarData.hWnd = _hWnd;
            _appBarData.uCallbackMessage = _callbackMessage;
            _appBarData.uEdge = AppBarAPI.ABE_BOTTOM;
            _appBarData.rc = new AppBarAPI.RECT(area.Left, area.Bottom - AlturaAppBar, area.Right, area.Bottom);

            AppBarAPI.SHAppBarMessage(AppBarAPI.ABM_NEW, ref _appBarData);
            _registrado = true;

            AppBarAPI.SHAppBarMessage(AppBarAPI.ABM_QUERYPOS, ref _appBarData);

            // ABE_BOTTOM: a borda inferior deve ficar na borda inferior da área de trabalho.
            _appBarData.rc.Bottom = area.Bottom;

            _reposicionandoAppBar = true;
            try
            {
                AppBarAPI.SHAppBarMessage(AppBarAPI.ABM_SETPOS, ref _appBarData);
            }
            finally
            {
                _reposicionandoAppBar = false;
            }

            AplicarBoundsAppBar(_appBarData.rc);
        }

        // Reposiciona quando a área de trabalho muda (ex.: barra de tarefas moveu).
        private void ReposicionarAppBar()
        {
            if (_reposicionandoAppBar)
                return;

            RedimensionarAppBar(Height);
        }

        // Etapa 3: altura ajustável arrastando a borda superior.
        // ABM_QUERYPOS -> ABM_SETPOS com a nova altura. Não recria a AppBar e não executa ABM_REMOVE.
        private void RedimensionarAppBar(int altura)
        {
            if (!_registrado || _hWnd == IntPtr.Zero)
                return;

            Rectangle area = Screen.PrimaryScreen.WorkingArea;

            _appBarData.cbSize = Marshal.SizeOf(typeof(AppBarAPI.APPBARDATA));
            _appBarData.hWnd = _hWnd;
            _appBarData.uCallbackMessage = _callbackMessage;
            _appBarData.uEdge = AppBarAPI.ABE_BOTTOM;

            // Ancora estavel: usa a borda inferior ATUAL da AppBar (posicao ja reservada
            // pelo shell), em vez da WorkingArea atual, que encolhe a cada ABM_SETPOS
            // e causava o loop de reposicionamento apos reabrir a AppBar.
            int bordaInferior = Bounds.Bottom;

            _appBarData.rc = new AppBarAPI.RECT(area.Left, bordaInferior - altura, area.Right, bordaInferior);

            AppBarAPI.SHAppBarMessage(AppBarAPI.ABM_QUERYPOS, ref _appBarData);

            // ABE_BOTTOM: a borda inferior permanece na posicao atual.
            _appBarData.rc.Bottom = bordaInferior;

            // Nada mudou: evita ABM_SETPOS redundante (e o ABN_POSCHANGED que ele causaria).
            if (_appBarData.rc.Left == Bounds.Left &&
                _appBarData.rc.Top == Bounds.Top &&
                _appBarData.rc.Right == Bounds.Right &&
                _appBarData.rc.Bottom == Bounds.Bottom)
            {
                return;
            }

            // Protecao contra reentrada: enquanto ABM_SETPOS esta em execucao,
            // novas notificacoes ABN_POSCHANGED sao ignoradas.
            if (_reposicionandoAppBar)
                return;

            _reposicionandoAppBar = true;
            try
            {
                AppBarAPI.SHAppBarMessage(AppBarAPI.ABM_SETPOS, ref _appBarData);
            }
            finally
            {
                _reposicionandoAppBar = false;
            }

            AplicarBoundsAppBar(_appBarData.rc);
        }

        private void AplicarBoundsAppBar(AppBarAPI.RECT rc)
        {
            SetBounds(rc.Left, rc.Top, rc.Right - rc.Left, rc.Bottom - rc.Top);
        }

        // ABM_REMOVE: libera a área reservada. Sempre chamado ao fechar.
        private void RemoverAppBar()
        {
            if (!_registrado || _hWnd == IntPtr.Zero)
                return;

            _registrado = false;

            AppBarAPI.APPBARDATA data = new AppBarAPI.APPBARDATA();
            data.cbSize = Marshal.SizeOf(typeof(AppBarAPI.APPBARDATA));
            data.hWnd = _hWnd;
            data.uCallbackMessage = _callbackMessage;

            AppBarAPI.SHAppBarMessage(AppBarAPI.ABM_REMOVE, ref data);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == _callbackMessage)
            {
                int notificacao = m.WParam.ToInt32();
                if (notificacao == AppBarAPI.ABN_POSCHANGED && !_reposicionandoAppBar)
                {
                    ReposicionarAppBar();
                }
            }
            base.WndProc(ref m);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            RemoverAppBar();
            base.OnFormClosed(e);
        }

        // Rede de segurança para encerramento do processo.
        protected override void OnHandleDestroyed(EventArgs e)
        {
            RemoverAppBar();
            base.OnHandleDestroyed(e);
        }

        // WS_EX_TOOLWINDOW: não aparece no Alt+Tab nem na barra de tarefas.
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x00000080;
                return cp;
            }
        }

        private void btnFechar_Click(object sender, EventArgs e)
        {
            AntesDeFechar?.Invoke(this, EventArgs.Empty);
            RemoverAppBar();
            Close();
            Dispose();
        }

        // Inicia o redimensionamento quando o botão esquerdo é pressionado na faixa superior.
        private void pnlResize_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
                return;

            _redimensionando = true;
            _posicaoInicialMouse = Control.MousePosition;
            _alturaInicialAppBar = Height;

            System.Diagnostics.Debug.WriteLine("[APPBAR] ResizeStart altura=" + Height);
        }

        // Durante o arraste: recalcula a altura, limita entre 70 e 400 e reposiciona a AppBar.
        private void pnlResize_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_redimensionando)
                return;

            int deltaY = Control.MousePosition.Y - _posicaoInicialMouse.Y;
            int novaAltura = _alturaInicialAppBar - deltaY;

            if (novaAltura < AlturaMinimaAppBar) novaAltura = AlturaMinimaAppBar;
            if (novaAltura > AlturaMaximaAppBar) novaAltura = AlturaMaximaAppBar;

            if (novaAltura == Height)
                return;

            RedimensionarAppBar(novaAltura);
            System.Diagnostics.Debug.WriteLine("[APPBAR] Resize altura=" + Height);
        }

        // Encerra o redimensionamento e mantém a nova altura (nesta etapa não persiste).
        private void pnlResize_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_redimensionando)
                return;

            _redimensionando = false;
            System.Diagnostics.Debug.WriteLine("[APPBAR] ResizeEnd altura=" + Height);
        }
    }
}
