using Hollowmere.Core;
using Hollowmere.Player;
using UnityEngine;
using UnityEngine.UI;

namespace Hollowmere.UI
{
    /// <summary>
    /// In-game overlay. Deliberately sparse - a horror HUD that shows everything removes
    /// the reason to look at the world. Only the two things the player actively spends
    /// (battery, stamina) get a permanent readout.
    /// </summary>
    public class HUD : MonoBehaviour
    {
        Text _prompt;
        Text _objective;
        Text _notification;
        Image _reticle;
        Image _batteryFill;
        Image _staminaFill;
        Image _breathFill;
        Image _vignette;
        Image _damageFlash;

        float _notificationTimer;

        public static HUD Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            UIBuilder.Stretch(go);

            var hud = go.AddComponent<HUD>();
            hud.Build(go.transform);
            return hud;
        }

        void Build(Transform root)
        {
            // Vignette sits behind everything else and darkens the frame as fear rises.
            _vignette = UIBuilder.MakePanel(root, "Vignette", new Color(0f, 0f, 0f, 0f));
            UIBuilder.Stretch(_vignette.gameObject);
            _vignette.sprite = RadialSprite();
            _vignette.type = Image.Type.Simple;
            _vignette.raycastTarget = false;

            _damageFlash = UIBuilder.MakePanel(root, "DamageFlash", new Color(0.5f, 0f, 0f, 0f));
            UIBuilder.Stretch(_damageFlash.gameObject);
            _damageFlash.raycastTarget = false;

            _reticle = UIBuilder.MakePanel(root, "Reticle", new Color(1f, 1f, 1f, 0.45f));
            UIBuilder.Place(_reticle.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(5f, 5f));
            _reticle.raycastTarget = false;

            _prompt = UIBuilder.MakeLabel(root, "Prompt", "", 26, TextAnchor.MiddleCenter);
            UIBuilder.Place(_prompt.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(0f, -70f), new Vector2(900f, 40f));

            _objective = UIBuilder.MakeLabel(root, "Objective", "", 24, TextAnchor.UpperLeft, UIBuilder.Dim);
            UIBuilder.Place(_objective.gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -40f), new Vector2(700f, 90f));

            _notification = UIBuilder.MakeLabel(root, "Notification", "", 26, TextAnchor.UpperCenter, UIBuilder.Accent);
            UIBuilder.Place(_notification.gameObject, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -120f), new Vector2(900f, 40f));

            _batteryFill = UIBuilder.MakeBar(root, "Battery", new Vector2(40f, 46f), new Vector2(260f, 16f),
                new Color(0.95f, 0.78f, 0.30f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _staminaFill = UIBuilder.MakeBar(root, "Stamina", new Vector2(40f, 22f), new Vector2(260f, 10f),
                new Color(0.55f, 0.75f, 0.95f), new Vector2(0f, 0f), new Vector2(0f, 0f));
            _breathFill = UIBuilder.MakeBar(root, "Breath", new Vector2(40f, 8f), new Vector2(260f, 6f),
                new Color(0.75f, 0.85f, 0.80f), new Vector2(0f, 0f), new Vector2(0f, 0f));

            var torchLabel = UIBuilder.MakeLabel(root, "BatteryLabel", "TORCH", 15, TextAnchor.LowerLeft, UIBuilder.Dim);
            UIBuilder.Place(torchLabel.gameObject, Vector2.zero, Vector2.zero,
                new Vector2(40f, 66f), new Vector2(200f, 20f));
        }

        void LateUpdate()
        {
            var player = PlayerController.Instance;
            var gm = GameManager.Instance;
            bool playing = gm != null && gm.IsPlaying && player != null;

            SetVisible(playing);
            if (!playing) return;

            UpdatePrompt(player);
            UpdateMeters(player);
            UpdateVignette(player);
            UpdateNotification();

            if (_objective != null) _objective.text = gm.ObjectiveText;
        }

        void SetVisible(bool visible)
        {
            if (_reticle != null) _reticle.enabled = visible;
            if (_prompt != null) _prompt.enabled = visible;
            if (_objective != null) _objective.enabled = visible;
            if (_notification != null) _notification.enabled = visible;
            if (_vignette != null) _vignette.enabled = visible;

            foreach (var bar in new[] { _batteryFill, _staminaFill, _breathFill })
            {
                if (bar == null) continue;
                bar.transform.parent.gameObject.SetActive(visible);
            }
        }

        void UpdatePrompt(PlayerController player)
        {
            if (_prompt == null) return;

            var interactor = player.GetComponent<Interactor>();
            if (interactor == null || interactor.Current == null)
            {
                _prompt.text = "";
                return;
            }

            _prompt.text = "[E] " + interactor.Current.Prompt;
            _prompt.color = interactor.CurrentIsAvailable ? UIBuilder.Ink : UIBuilder.Dim;
        }

        void UpdateMeters(PlayerController player)
        {
            var status = player.GetComponent<PlayerStatus>();

            if (_batteryFill != null && Flashlight.Instance != null)
            {
                float battery = Flashlight.Instance.Battery;
                _batteryFill.fillAmount = battery;
                // Bar turns red before the light actually fails, as a warning.
                _batteryFill.color = battery < 0.25f
                    ? Color.Lerp(UIBuilder.Danger, new Color(0.95f, 0.78f, 0.30f), battery / 0.25f)
                    : new Color(0.95f, 0.78f, 0.30f);
            }

            if (status == null) return;
            if (_staminaFill != null) _staminaFill.fillAmount = status.Stamina;
            if (_breathFill != null)
            {
                _breathFill.fillAmount = status.Breath;
                _breathFill.color = status.IsHoldingBreath
                    ? new Color(0.95f, 0.55f, 0.35f)
                    : new Color(0.75f, 0.85f, 0.80f);
            }
        }

        void UpdateVignette(PlayerController player)
        {
            if (_vignette == null) return;

            var status = player.GetComponent<PlayerStatus>();
            float fear = status != null ? status.Fear : 0f;

            // Pulse in time with the heartbeat so the two read as one effect.
            float pulse = 1f + Mathf.Sin(Time.time * Mathf.Lerp(1.2f, 5f, fear)) * 0.08f * fear;
            float alpha = Mathf.Clamp01(fear * 0.85f * pulse);

            _vignette.color = new Color(0f, 0f, 0f, alpha);
        }

        void UpdateNotification()
        {
            if (_notification == null) return;

            if (_notificationTimer > 0f)
            {
                _notificationTimer -= Time.deltaTime;
                var c = _notification.color;
                _notification.color = new Color(c.r, c.g, c.b, Mathf.Clamp01(_notificationTimer));
            }
            else
            {
                _notification.text = "";
            }
        }

        public void Notify(string message, float duration = 2.5f)
        {
            if (_notification == null) return;
            _notification.text = message;
            _notification.color = UIBuilder.Accent;
            _notificationTimer = duration;
        }

        public void FlashDamage()
        {
            if (_damageFlash == null) return;
            _damageFlash.color = new Color(0.5f, 0f, 0f, 0.75f);
        }

        void Update()
        {
            if (_damageFlash == null) return;
            var c = _damageFlash.color;
            if (c.a > 0f) _damageFlash.color = new Color(c.r, c.g, c.b, Mathf.MoveTowards(c.a, 0f, Time.unscaledDeltaTime));
        }

        /// <summary>Radial black-to-transparent sprite used as the vignette mask.</summary>
        static Sprite RadialSprite()
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[size * size];
            var centre = new Vector2(size * 0.5f, size * 0.5f);
            float maxDistance = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), centre) / maxDistance;
                    // Opaque at the edges, clear through the middle third.
                    float a = Mathf.Clamp01((d - 0.35f) / 0.65f);
                    a = a * a;
                    pixels[y * size + x] = new Color32(0, 0, 0, (byte)(a * 255f));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
