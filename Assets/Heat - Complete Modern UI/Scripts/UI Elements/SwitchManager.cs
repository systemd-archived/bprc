using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace Michsky.UI.Heat
{
    public class SwitchManager : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, ISubmitHandler
    {
        // Resources
        [SerializeField] private Animator switchAnimator;
        [SerializeField] private CanvasGroup highlightCG;

        // Saving
        public bool saveValue = false;
        public string saveKey = "My Switch";

        // Settings
        public bool isOn = true;
        public bool isInteractable = true;
        public bool invokeOnEnable = true;
        public bool useSounds = true;
        public bool useUINavigation = false;
        [Range(1, 15)] public float fadingMultiplier = 8;

        // Events
        [SerializeField] public SwitchEvent onValueChanged = new SwitchEvent();
        public UnityEvent onEvents = new UnityEvent();
        public UnityEvent offEvents = new UnityEvent();

        [System.Serializable]
        public class SwitchEvent : UnityEvent<bool> { }

        bool isInitialized = false;
        bool hasStarted;

        void Awake()
        {
            switchAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            if (saveValue) { GetSavedData(); }
            else
            {
                if (gameObject.activeInHierarchy)
                {
                    StopCoroutine("DisableAnimator");
                    StartCoroutine("DisableAnimator");
                }

                switchAnimator.enabled = true;

                if (isOn) { switchAnimator.Play("On Instant"); }
                else { switchAnimator.Play("Off Instant"); }
            }

            if (gameObject.GetComponent<Image>() == null)
            {
                Image raycastImg = gameObject.AddComponent<Image>();
                raycastImg.color = new Color(0, 0, 0, 0);
                raycastImg.raycastTarget = true;
            }

            if (useUINavigation) { AddUINavigation(); }
            if (highlightCG == null) { highlightCG = new GameObject().AddComponent<CanvasGroup>(); highlightCG.transform.SetParent(transform); highlightCG.gameObject.name = "Highlighted"; }

            if (invokeOnEnable && isOn) { onEvents.Invoke(); onValueChanged.Invoke(isOn); }
            else if (invokeOnEnable && !isOn) { offEvents.Invoke(); onValueChanged.Invoke(isOn); }

            isInitialized = true;
        }

        void OnEnable()
        {
            if (UIManagerAudio.instance == null) { useSounds = false; }
            if (isInitialized) { UpdateUI(); }
        }

        void Start()
        {
            hasStarted = true;
            UpdateUI();
        }

        void GetSavedData()
        {
            if (gameObject.activeInHierarchy)
            {
                StopCoroutine("DisableAnimator");
                StartCoroutine("DisableAnimator");
            }

            switchAnimator.enabled = true;

            if (PlayerPrefs.GetString("Switch_" + saveKey) == "" || !PlayerPrefs.HasKey("Switch_" + saveKey))
            {
                if (isOn) { switchAnimator.Play("Off"); PlayerPrefs.SetString("Switch_" + saveKey, "true"); }
                else { switchAnimator.Play("Off"); PlayerPrefs.SetString("Switch_" + saveKey, "false"); }
            }
            else if (PlayerPrefs.GetString("Switch_" + saveKey) == "true") { switchAnimator.Play("Off"); isOn = true; }
            else if (PlayerPrefs.GetString("Switch_" + saveKey) == "false") { switchAnimator.Play("Off"); isOn = false; }
        }

        public void AddUINavigation()
        {
            UnityEngine.UI.Button navButton = gameObject.AddComponent<UnityEngine.UI.Button>();
            navButton.transition = Selectable.Transition.None;
            Navigation customNav = new Navigation();
            customNav.mode = Navigation.Mode.Automatic;
            navButton.navigation = customNav;
        }

        public void AnimateSwitch()
        {
            if (gameObject.activeInHierarchy)
            {
                StopCoroutine("DisableAnimator");
                StartCoroutine("DisableAnimator");
            }

            switchAnimator.enabled = true;

            if (isOn)
            {
                PlayState("Off");
                isOn = false;
                offEvents.Invoke();

                if (saveValue)
                {
                    PlayerPrefs.SetString("Switch_" + saveKey, "false");
                }
            }

            else
            {
                PlayState("On");
                isOn = true;
                onEvents.Invoke();

                if (saveValue)
                {
                    PlayerPrefs.SetString("Switch_" + saveKey, "true");
                }
            }

            onValueChanged.Invoke(isOn);
        }

        public void SetOn()
        {
            if (saveValue) { PlayerPrefs.SetString("Switch_" + saveKey, "true"); }
            if (gameObject.activeInHierarchy)
            {
                StopCoroutine("DisableAnimator");
                StartCoroutine("DisableAnimator");
            }

            switchAnimator.enabled = true;
            PlayState("On");

            isOn = true;
            onEvents.Invoke();
            onValueChanged.Invoke(true);
        }

        public void SetOff()
        {
            if (saveValue) { PlayerPrefs.SetString("Switch_" + saveKey, "false"); }
            if (gameObject.activeInHierarchy)
            {
                StopCoroutine("DisableAnimator");
                StartCoroutine("DisableAnimator");
            }

            switchAnimator.enabled = true;
            PlayState("Off");

            isOn = false;
            offEvents.Invoke();
            onValueChanged.Invoke(false);
        }

        public void UpdateUI()
        {
            if (gameObject.activeInHierarchy)
            {
                StopCoroutine("DisableAnimator");
                StartCoroutine("DisableAnimator");
            }

            switchAnimator.enabled = true;

            PlayState(isOn ? "On Instant" : "Off Instant");
        }

        private void PlayState(string state)
        {
            if (!switchAnimator.gameObject.activeInHierarchy) { return; }
            switchAnimator.Play(state, 0, 0);
            if (hasStarted) { switchAnimator.Update(0); }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isInteractable || eventData.button != PointerEventData.InputButton.Left) { return; }
            if (useSounds) { UIManagerAudio.instance.audioSource.PlayOneShot(UIManagerAudio.instance.UIManagerAsset.clickSound); }

            AnimateSwitch();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!isInteractable) { return; }
            if (useSounds) { UIManagerAudio.instance.audioSource.PlayOneShot(UIManagerAudio.instance.UIManagerAsset.hoverSound); }

            StartCoroutine("SetHighlight");
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isInteractable)
                return;

            StartCoroutine("SetNormal");
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!isInteractable)
                return;

            StartCoroutine("SetHighlight");
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!isInteractable)
                return;

            StartCoroutine("SetNormal");
        }

        public void OnSubmit(BaseEventData eventData)
        {
            if (!isInteractable)
                return;

            AnimateSwitch();
            StartCoroutine("SetNormal");
        }

        IEnumerator DisableAnimator()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            switchAnimator.enabled = false;
        }

        IEnumerator SetNormal()
        {
            StopCoroutine("SetHighlight");

            while (highlightCG.alpha > 0.01f)
            {
                highlightCG.alpha -= Time.unscaledDeltaTime * fadingMultiplier;
                yield return null;
            }

            highlightCG.alpha = 0;
        }

        IEnumerator SetHighlight()
        {
            StopCoroutine("SetNormal");

            while (highlightCG.alpha < 0.99f)
            {
                highlightCG.alpha += Time.unscaledDeltaTime * fadingMultiplier;
                yield return null;
            }

            highlightCG.alpha = 1;
        }
    }
}
