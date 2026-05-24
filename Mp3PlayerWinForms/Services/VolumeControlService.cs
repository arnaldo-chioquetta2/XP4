using System;
using System.Windows.Forms;
using XP3.Services;

namespace Mp3PlayerWinForms.Services
{
    public class VolumeControlService
    {
        private readonly AudioPlayerService _audioPlayer;
        private readonly Label _statusLabel;

        public VolumeControlService(AudioPlayerService audioPlayer, Label statusLabel)
        {
            _audioPlayer = audioPlayer;
            _statusLabel = statusLabel;
        }

        public void IncreaseVolume()
        {
            if (_audioPlayer.Volume < 1.0f)
            {
                _audioPlayer.Volume = Math.Min(_audioPlayer.Volume + 0.1f, 1.0f);
                UpdateStatusLabel();
            }
        }

        public void DecreaseVolume()
        {
            if (_audioPlayer.Volume > 0.1f)
            {
                _audioPlayer.Volume -= 0.1f;
                UpdateStatusLabel();
            }
        }

        //public void DecreaseVolume()
        //{
        //    if (_audioPlayer.Volume > 0.1f) // Diminui apenas se acima do mínimo para evitar atualizações desnecessárias
        //    {
        //        _audioPlayer.Volume -= 0.1f;
        //        UpdateStatusLabel();
        //    }
        //}

        private void UpdateStatusLabel()
        {
            _statusLabel.Text = $"Volume: {(int)(_audioPlayer.Volume * 100)}%";
        }
    }
}

