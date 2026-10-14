using System;
using DG.Tweening;
using UnityEngine;

namespace RecycleLife.Unity
{
    /// <summary>
    /// 몬스터·아이템(물약) 한 마리의 "느낌"만 담당한다(포켓던전류 연출). 로직 위치·HP는
    /// 전혀 모른다 — 이 오브젝트(Visual)의 localPosition / localRotation / localScale / 색만
    /// 건드리고, 끝나면 반드시 기본값으로 돌아온다.
    ///
    /// <b>피벗이 바닥 기준이 되도록</b> 이 트랜스폼 자체는 칸의 "발 밑"에 둔다
    /// (<see cref="RestLocalPosition"/>, 부모인 Root 기준 -0.5칸). 실제 스프라이트는
    /// 자식(Sprite, localPosition.y=+0.5)에 둬서, 숨쉬기 스케일이 이 트랜스폼을 늘려도
    /// 발 위치는 그대로고 위쪽만 자란다. 피벗을 바꾸려면 Sprite 자식의 y값만 고치면 된다.
    ///
    /// 네 채널(상시 Idle 스케일 / 1회성 스케일(스폰·착지·사망·획득) / 위치(피격·공격) / 색(피격·사망))을
    /// 따로 추적해 서로 안 건드리는 애니메이션끼리는 동시에 돌고, 같은 채널을 다시 쓰는 새
    /// 연출이 들어오면 이전 트윈을 죽이고 기본값에서 다시 시작한다(값 누적 방지).
    /// </summary>
    public sealed class MonsterVisualFeedback : MonoBehaviour
    {
        private static readonly Vector3 RestLocalPosition = new Vector3(0f, -0.5f, 0f);
        private static readonly Vector3 RestLocalScale = Vector3.one;

        [Header("연결")]
        [SerializeField, Tooltip("실제로 그리는 스프라이트. 피벗을 바닥에 맞추려고 이 오브젝트가 아니라 " +
                                 "자식(Sprite)에 둔다. 비우면 자식에서 찾는다.")]
        private SpriteRenderer spriteRenderer;

        /// <summary>BoardView가 가격 강조 등으로 직접 건드려야 할 때를 위해 연다.</summary>
        public SpriteRenderer SpriteRenderer => spriteRenderer;

        private MonsterFeedbackSettings _settings;
        private Color _baseColor = Color.white;

        private Tween _idleTween;
        private Sequence _scaleActionSequence;
        private Sequence _moveSequence;
        private Sequence _colorSequence;
        private Sequence _terminalSequence;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        private void OnDisable()
        {
            KillAll();
        }

        /// <summary>
        /// 종류별 수치를 꽂는다. 풀링으로 슬롯이 재사용될 때마다(다른 종류로 바뀔 때마다)
        /// BoardView가 매번 다시 부른다 — 설정만 갈아 끼우고 애니메이션은 건드리지 않는다.
        /// </summary>
        public void Configure(MonsterFeedbackSettings settings)
        {
            _settings = settings;
        }

        /// <summary>
        /// BoardView가 색을 칠한 직후 불러서 플래시가 돌아갈 "원래 색"을 알려 준다.
        /// 지금 플래시가 도는 중이 아니면 바로 반영한다.
        /// </summary>
        public void SetBaseColor(Color color)
        {
            _baseColor = color;
            if (spriteRenderer != null && _colorSequence == null)
            {
                spriteRenderer.color = color;
            }
        }

        // ── 1. Idle (상시) ──────────────────────────────────────────────

        private void StartIdle()
        {
            KillIdle();

            if (_settings == null)
            {
                return;
            }

            transform.localScale = RestLocalScale;
            transform.localPosition = RestLocalPosition;

            float periodJitter = UnityEngine.Random.Range(-_settings.IdlePeriodJitter, _settings.IdlePeriodJitter);
            float phaseDelay;
            Sequence loop = DOTween.Sequence();

            if (_settings.Idle == MonsterFeedbackSettings.IdleMode.Float)
            {
                float period = Mathf.Max(0.01f, _settings.FloatPeriod * (1f + periodJitter));
                phaseDelay = UnityEngine.Random.Range(0f, _settings.IdlePhaseJitter) * period;

                float baseY = RestLocalPosition.y;
                loop.Append(transform.DOLocalMoveY(baseY + _settings.FloatDistance, period * 0.5f).SetEase(_settings.FloatEase));
                loop.Append(transform.DOLocalMoveY(baseY - _settings.FloatDistance, period * 0.5f).SetEase(_settings.FloatEase));
            }
            else
            {
                float period = Mathf.Max(0.01f, _settings.BreathePeriod * (1f + periodJitter));
                phaseDelay = UnityEngine.Random.Range(0f, _settings.IdlePhaseJitter) * period;

                loop.Append(transform.DOScale(new Vector3(_settings.BreatheScaleXMin, _settings.BreatheScaleYMax, 1f), period * 0.5f).SetEase(_settings.BreatheEase));
                loop.Append(transform.DOScale(RestLocalScale, period * 0.5f).SetEase(_settings.BreatheEase));
            }

            loop.SetLoops(-1, LoopType.Restart);
            loop.SetDelay(phaseDelay);
            _idleTween = loop;
        }

        private void KillIdle()
        {
            if (_idleTween != null && _idleTween.IsActive())
            {
                _idleTween.Kill();
            }

            _idleTween = null;
        }

        // ── 2. 스폰 / 착지 ──────────────────────────────────────────────

        /// <summary>처음 만들어졌을 때 한 번. scale 0 -> 1로 팝업한 뒤 Idle을 시작한다.</summary>
        public void PlaySpawn()
        {
            KillIdle();
            KillScaleAction();
            transform.localPosition = RestLocalPosition;

            if (_settings == null)
            {
                transform.localScale = RestLocalScale;
                return;
            }

            transform.localScale = Vector3.zero;

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOScale(RestLocalScale, _settings.SpawnPopDuration).SetEase(_settings.SpawnPopEase));
            seq.OnComplete(StartIdle);
            _scaleActionSequence = seq;
        }

        /// <summary>중력으로 한 칸(혹은 여러 칸) 떨어져 자리를 잡을 때마다. 몇 번이든 다시 불러도 된다.</summary>
        public void PlayLand()
        {
            KillIdle();
            KillScaleAction();

            if (_settings == null)
            {
                transform.localScale = RestLocalScale;
                return;
            }

            transform.localScale = new Vector3(_settings.LandSquashScaleX, _settings.LandSquashScaleY, 1f);

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOScale(RestLocalScale, _settings.LandRecoverDuration).SetEase(_settings.LandRecoverEase));
            seq.OnComplete(StartIdle);
            _scaleActionSequence = seq;
        }

        private void KillScaleAction()
        {
            if (_scaleActionSequence != null && _scaleActionSequence.IsActive())
            {
                _scaleActionSequence.Kill();
            }

            _scaleActionSequence = null;
        }

        // ── 3. 피격 ─────────────────────────────────────────────────────

        /// <summary>
        /// 피해를 입었다(죽지 않았을 때만 호출해야 자연스럽다 — 죽었으면 PlayDeath를 쓴다).
        /// <paramref name="fromDir"/>은 공격이 날아온(= 플레이어가 움직인) 방향. 그 방향으로
        /// 계속 떠밀리듯 살짝 넉백됐다가, Elastic 이징으로 흔들리며 돌아온다.
        /// </summary>
        public void PlayHit(Vector2Int fromDir)
        {
            if (_settings == null)
            {
                return;
            }

            KillMove();
            KillColor();
            transform.localPosition = RestLocalPosition;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = _baseColor;
            }

            Vector3 dir = new Vector3(fromDir.x, fromDir.y, 0f);
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
            }

            Vector3 pushed = RestLocalPosition + (dir * _settings.HitPushDistance);

            Sequence move = DOTween.Sequence();
            move.Append(transform.DOLocalMove(pushed, _settings.HitPushOutDuration).SetEase(Ease.OutQuad));
            move.Append(transform.DOLocalMove(RestLocalPosition, _settings.HitReturnDuration)
                .SetEase(Ease.OutElastic, _settings.HitShakeAmplitude, _settings.HitShakePeriod));
            _moveSequence = move;

            if (spriteRenderer != null)
            {
                Sequence color = DOTween.Sequence();
                color.Append(TweenColor(_settings.HitFlashColor, _settings.HitFlashDuration * 0.5f));
                color.Append(TweenColor(_baseColor, _settings.HitFlashDuration * 0.5f));
                _colorSequence = color;
            }
        }

        /// <summary>
        /// 색만 바꾸는 트윈. SpriteRenderer.DOColor 숏컷은 이 프로젝트의 DOTween 설치에서
        /// Sprites 모듈이 꺼져 있어 못 쓴다 — 모든 설치에서 동작하는 범용 DOTween.To로 대신한다.
        /// </summary>
        private Tweener TweenColor(Color target, float duration)
        {
            return DOTween.To(() => spriteRenderer.color, c => spriteRenderer.color = c, target, duration);
        }

        // ── 4. 공격 (반격 범프) ─────────────────────────────────────────

        /// <summary>
        /// 플레이어에게 반격한다(부딪힌 그 한 마리만, CORE_COMBAT.md §5). <paramref name="onHit"/>은
        /// 돌진이 끝에 닿는 순간 호출된다 — 플레이어 쪽 피격 이펙트 타이밍을 맞추고 싶을 때 쓰는 훅.
        /// </summary>
        public void PlayAttack(Vector2Int towardPlayerDir, Action onHit)
        {
            if (_settings == null)
            {
                onHit?.Invoke();
                return;
            }

            KillMove();
            transform.localPosition = RestLocalPosition;

            if (towardPlayerDir.x != 0 && spriteRenderer != null)
            {
                bool right = towardPlayerDir.x > 0;
                spriteRenderer.flipX = _settings.ArtFacesLeft ? right : !right;
            }

            Vector3 dir = new Vector3(towardPlayerDir.x, towardPlayerDir.y, 0f);
            if (dir.sqrMagnitude > 0.0001f)
            {
                dir.Normalize();
            }

            Vector3 lunge = RestLocalPosition + (dir * _settings.AttackLungeDistance);

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOLocalMove(lunge, _settings.AttackOutDuration).SetEase(_settings.AttackOutEase));
            seq.AppendCallback(() => onHit?.Invoke());
            seq.Append(transform.DOLocalMove(RestLocalPosition, _settings.AttackBackDuration).SetEase(_settings.AttackBackEase));
            _moveSequence = seq;
        }

        private void KillMove()
        {
            if (_moveSequence != null && _moveSequence.IsActive())
            {
                _moveSequence.Kill();
            }

            _moveSequence = null;
        }

        private void KillColor()
        {
            if (_colorSequence != null && _colorSequence.IsActive())
            {
                _colorSequence.Kill();
            }

            _colorSequence = null;
        }

        // ── 5. 사망 / 7. 획득 ────────────────────────────────────────────

        /// <summary>
        /// 죽었다. 피격 플래시 -> 살짝 커졌다가 0으로 팝 하며 사라진다. 그리드에서는 이미
        /// 지워진 뒤이므로, <paramref name="onComplete"/>가 끝나야 BoardView가 실제로
        /// 풀에 돌려준다 — 연출 도중에는 이 칸을 아무도 재사용하지 않는다.
        /// </summary>
        public void PlayDeath(Action onComplete)
        {
            KillAll();
            transform.localPosition = RestLocalPosition;
            transform.localScale = RestLocalScale;

            if (_settings == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = _baseColor;
            }

            Sequence seq = DOTween.Sequence();
            if (spriteRenderer != null)
            {
                seq.Append(TweenColor(_settings.HitFlashColor, _settings.DeathFlashDuration * 0.5f));
                seq.Append(TweenColor(_baseColor, _settings.DeathFlashDuration * 0.5f));
            }
            else
            {
                seq.AppendInterval(_settings.DeathFlashDuration);
            }

            seq.Append(transform.DOScale(Vector3.one * _settings.DeathPopScale, _settings.DeathPopDuration * 0.4f).SetEase(Ease.OutQuad));
            seq.Append(transform.DOScale(Vector3.zero, _settings.DeathPopDuration * 0.6f).SetEase(_settings.DeathPopEase));
            seq.OnComplete(() => onComplete?.Invoke());

            _terminalSequence = seq;
        }

        /// <summary>
        /// 먹혔다(물약 등 소비 아이템). scale 1 -> 1.3 -> 0 팝 + 위로 살짝 떠오르며 사라진다.
        /// 죽음과 마찬가지로 <paramref name="onComplete"/>가 끝나야 풀에 돌아간다.
        /// </summary>
        public void PlayPickup(Action onComplete)
        {
            KillAll();
            transform.localPosition = RestLocalPosition;
            transform.localScale = RestLocalScale;

            if (_settings == null)
            {
                onComplete?.Invoke();
                return;
            }

            Vector3 risen = RestLocalPosition + new Vector3(0f, _settings.PickupRiseDistance, 0f);

            Sequence seq = DOTween.Sequence();
            seq.Append(transform.DOScale(Vector3.one * _settings.PickupPopScale, _settings.PickupDuration * 0.5f).SetEase(_settings.PickupEase));
            seq.Join(transform.DOLocalMove(risen, _settings.PickupDuration).SetEase(_settings.PickupEase));
            seq.Append(transform.DOScale(Vector3.zero, _settings.PickupDuration * 0.5f).SetEase(Ease.InQuad));
            seq.OnComplete(() => onComplete?.Invoke());

            _terminalSequence = seq;
        }

        /// <summary>진행 중인 연출을 전부 끊고 기본 상태로 되돌린다. 풀에 반납되기 전에 부른다.</summary>
        public void SnapIdle()
        {
            KillAll();
            transform.localPosition = RestLocalPosition;
            transform.localRotation = Quaternion.identity;
            transform.localScale = RestLocalScale;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = _baseColor;
            }
        }

        private void KillAll()
        {
            KillIdle();
            KillScaleAction();
            KillMove();
            KillColor();

            if (_terminalSequence != null && _terminalSequence.IsActive())
            {
                _terminalSequence.Kill();
            }

            _terminalSequence = null;
        }
    }
}
