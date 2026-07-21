namespace DragonPlus.SpineExtensions
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using Spine.Unity;
#if UNITY_EDITOR
    using UnityEditor;
    using System.Reflection;
#endif
    public class SpineAnimationChange : MonoBehaviour
    {
        public SkeletonGraphic m_skeletonGraphic;
        public int AnimationIndex_Int;
        public bool AnimationLoop_Bool;
        private int lastAnimationIndex_Int = -1;
        public string PlayName = "";
        private Spine.TrackEntry currentTrackEntry;


        void OnEnable()
        {

            // Debug.LogError($"Arthur----->OnEnable,Name={gameObject.name},index={AnimationIndex_Int}");
            CheckAnimationChange(true);
        }
        void Update()
        {
            CheckAnimationChange();
        }
        //检查是否切换动画
        void CheckAnimationChange(bool forcePlay = false)
        {
            if (forcePlay)
            {
                if (AnimationIndex_Int >= 0 && AnimationIndex_Int < m_skeletonGraphic.SkeletonData.Animations.Items.Length)
                {
                    PlayName = m_skeletonGraphic.SkeletonData.Animations.Items[AnimationIndex_Int].Name;
                    if (currentTrackEntry != null && !currentTrackEntry.IsComplete)
                    {
                        m_skeletonGraphic.AnimationState.ClearTrack(0);
                    }

                    currentTrackEntry = m_skeletonGraphic.AnimationState.SetAnimation(0, PlayName, AnimationLoop_Bool);
                    m_skeletonGraphic.AnimationState.Update(0);

                    lastAnimationIndex_Int = AnimationIndex_Int;
#if UNITY_EDITOR
                    lastTime = 0;
#endif
                }
            }
            else
            {
                if (lastAnimationIndex_Int != AnimationIndex_Int)
                {
                    if (AnimationIndex_Int >= 0 && AnimationIndex_Int < m_skeletonGraphic.SkeletonData.Animations.Items.Length)
                    {
                        PlayName = m_skeletonGraphic.SkeletonData.Animations.Items[AnimationIndex_Int].Name;
                        //第一次播动画
                        if (lastAnimationIndex_Int < 0)
                        {
                            if (currentTrackEntry != null && !currentTrackEntry.IsComplete)
                            {
                                m_skeletonGraphic.AnimationState.ClearTrack(0);
                            }
                        }

                        currentTrackEntry = m_skeletonGraphic.AnimationState.SetAnimation(0, PlayName, AnimationLoop_Bool);
                        //第一次播动画，避免跳帧
                        if (lastAnimationIndex_Int < 0)
                        {
                            m_skeletonGraphic.AnimationState.Update(0);
                        }

                        lastAnimationIndex_Int = AnimationIndex_Int;
#if UNITY_EDITOR
                        lastTime = 0;
#endif
                    }
                }
            }

        }

#if UNITY_EDITOR
        #region  Editor
        static FieldInfo animEditor;
        static PropertyInfo IsPlaying;
        static PropertyInfo Time;

        static EditorWindow animationWindowEditor;
        static object controlInterface;

        static Type AnimationKeyTimeType;
        static bool initEditor = false;
        private float lastTime = 0;
        public static bool InitInEditor()
        {
            if (initEditor) return false;
            animEditor = SpineEditorHelp.GetAnimEditor(ref animationWindowEditor);
            IsPlaying = SpineEditorHelp.P_IsPlaying(animEditor);
            Time = SpineEditorHelp.P_Time(animEditor);
            controlInterface = SpineEditorHelp.GetcontrolInterface(animEditor, animationWindowEditor);
            AnimationKeyTimeType = typeof(EditorWindow).Assembly.GetType("UnityEditorInternal.AnimationKeyTime");
            initEditor = true;
            return true;
        }
        public static bool ReleaseEditor()
        {
            if (!initEditor) return false;
            animEditor = null;
            IsPlaying = null;
            Time = null;
            controlInterface = null;
            AnimationKeyTimeType = null;
            initEditor = false;
            return true;
        }

        public void StartInEditor()
        {
            OnEnable();
        }
        //编辑器下的Update
        public void OnAnimationEditorUpdate()
        {
            //Debug.Log("Arthur----->OnAnimationEditorUpdate");
            if (animEditor != null)
            {
                PropertyInfo apInfo = AnimationKeyTimeType.GetProperty("time");
                object value = Time.GetValue(controlInterface);
                float time = ((float)apInfo.GetValue(value));
                float deltaTime = time - lastTime;
                Debug.Log($"Arthur----->OnAnimationEditorUpdate,time={time},deltaTime={deltaTime}");
                CheckAnimationChange();
                if (deltaTime > 0)
                {
                    //倒着播会有点问题，因为设置的动画不一样
                    m_skeletonGraphic.AnimationState.Update(deltaTime);
                }
                lastTime = time;
                m_skeletonGraphic.LateUpdate();

            }
        }
        #endregion
#endif
    }
}

