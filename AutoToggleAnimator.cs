using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public class AutoToggleAnimator : EditorWindow
{
  private VRCAvatarDescriptor avatar;
  private VRCAvatarDescriptor.CustomAnimLayer animLayer;
  private VRCExpressionParameters avatarParams;
  private VRCExpressionsMenu avatarMenu;
  private RuntimeAnimatorController targetAnimator;
  private AnimatorController animController;
  private Animator animator;

  private AnimationClip onAnimClip;
  private AnimationClip offAnimClip;

  private bool makeMenu;

  private string paramName;
  private string menuName;

  private string errorMsg;

  [MenuItem("coco/AutoFXToggle")]
  private static void Init()
  {
    var window = (AutoToggleAnimator) EditorWindow.GetWindow(typeof(AutoToggleAnimator));
    window.Show();
  }

  private void OnGUI()
  {
    GUILayout.Label("Target Avatar", EditorStyles.boldLabel);
    EditorGUI.BeginChangeCheck();
    this.avatar = EditorGUILayout.ObjectField("Avatar", avatar, typeof(VRCAvatarDescriptor), true) as VRCAvatarDescriptor;
    GUILayout.Label("Target Animations", EditorStyles.boldLabel);
    this.onAnimClip = EditorGUILayout.ObjectField("On Animation", onAnimClip, typeof(AnimationClip), true) as AnimationClip;
    this.offAnimClip = EditorGUILayout.ObjectField("Off Animation", offAnimClip, typeof(AnimationClip), true) as AnimationClip;
    
    GUILayout.Label("Parameter Name", EditorStyles.boldLabel);
    EditorGUI.BeginChangeCheck();
    this.paramName = EditorGUILayout.TextField("Parameter name", this.paramName);
    EditorGUI.EndChangeCheck();
    
    GUILayout.Label("Menu Name", EditorStyles.boldLabel);
    EditorGUI.BeginChangeCheck();
    this.menuName = EditorGUILayout.TextField("Menu Name", this.menuName);
    EditorGUI.EndChangeCheck();

    if (CheckFields())
    {
      SetErrorField("");
      this.makeMenu = GUILayout.Toggle(this.makeMenu, "Make new avatar menu and apply");
      if (GUILayout.Button("Apply"))
      {
        if (!MakeAnimator()) return;
        if (!EditVrcDescriptor()) return;
        ResetFields();
        EditorGUILayout.LabelField("SUCCESS");
      }
    }

    if (GUILayout.Button("Reset"))
    {
      ResetFields();
    }
    
    EditorGUILayout.LabelField(errorMsg);
  }

  private bool CheckFields()
  {
    if (this.avatar == null)
    {
      SetErrorField("Avatar not registered");
      return false;
    }
    
    this.animLayer = this.avatar.baseAnimationLayers[4];  // FX
    this.avatarMenu = this.avatar.expressionsMenu;
    this.avatarParams = this.avatar.expressionParameters;
    
    if (animLayer.animatorController == null)
    {
      return SetErrorField("FX animator not registered");
    }
    
    this.targetAnimator = animLayer.animatorController;
    this.animController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GetAssetPath(targetAnimator));
    
    if (this.animController == null)
    {
      return SetErrorField("FX animator controller not registered");
    }
    
    if (this.onAnimClip == null)
    {
      return SetErrorField("off animation is empty");
    }

    if (this.offAnimClip == null)
    {
      return SetErrorField("Off animation is empty");
    }

    if (this.avatarMenu == null)
    {
      return SetErrorField("Menu name is empty");
    }

    if (this.avatarParams == null)
    {
      return SetErrorField("Parameter name is empty");
    }

    if (this.paramName == "")
    {
      return SetErrorField("parameter name is empty");
    }

    if (this.menuName == "")
    {
      return SetErrorField("menu name is empty");
    }
    
    if (this.animController.parameters.Any(e => e.name == this.paramName))
    {
      return SetErrorField("Duplicate parameter name on FX Animator");
    }

    if (this.avatarMenu.controls.Any(e => e.parameter.name == this.paramName))
    {
      return SetErrorField("Duplicate parameter name on Avatar Menu");
    }

    if (this.avatarParams.parameters.Any(e => e.name == this.paramName))
    {
      return SetErrorField("Duplicate parameter name on Avatar Parameters");
    }

    return true;
  }

  private bool MakeAnimator()
  {
    try
    {
      var stateMachine = new AnimatorStateMachine();
      var onAnim = new AnimatorState()
      {
        name = this.menuName + " ON",
        motion = this.onAnimClip
      };

      var offAnim = new AnimatorState()
      {
        name = this.menuName + " OFF",
        motion = this.offAnimClip
      };

      stateMachine.AddState(onAnim, Vector3.left);
      stateMachine.AddState(offAnim, Vector3.right);

      var layer = new AnimatorControllerLayer
      {
        name = this.menuName,
        stateMachine = stateMachine,
        defaultWeight = 1
      };
      this.animController.AddLayer(layer);

      this.animController.AddParameter(this.paramName, AnimatorControllerParameterType.Bool);

      var onTransition = stateMachine.AddAnyStateTransition(onAnim);
      TransitionReset(onTransition);
      onTransition.AddCondition(AnimatorConditionMode.If, 1, this.paramName);

      var offTransition = stateMachine.AddAnyStateTransition(offAnim);
      TransitionReset(offTransition);
      offTransition.AddCondition(AnimatorConditionMode.IfNot, 1, this.paramName);

      return true;
    }
    catch (Exception e)
    {
      return SetErrorField(e.Message);
    }
  }

  private bool EditVrcDescriptor()
  {
    try
    {
      var parameters = this.avatarParams.parameters;
      var newParameters = new VRCExpressionParameters.Parameter[this.avatarParams.parameters.Length + 1];
      for (int i = 0; i < parameters.Length; ++i) newParameters[i] = parameters[i];

      newParameters[newParameters.Length - 1] = new VRCExpressionParameters.Parameter()
      {
        defaultValue = 1,
        name = this.paramName,
        saved = true,
        valueType = VRCExpressionParameters.ValueType.Bool
      };
      this.avatarParams.parameters = newParameters;

      if (this.makeMenu)
      {
        this.avatarMenu.controls.Add(new VRCExpressionsMenu.Control()
        {
          icon = null,
          name = this.menuName,
          type = VRCExpressionsMenu.Control.ControlType.Toggle,
          parameter = new VRCExpressionsMenu.Control.Parameter()
          {
            name = this.paramName
          },
          value = 1,
        });
      }

      return true;
    }
    catch (Exception e)
    {
      return SetErrorField(e.Message);
    }
  }

  private void ResetFields()
  {
    this.avatar = null;
    this.animLayer = default;
    this.avatarParams = null;
    this.avatarMenu = null;
    this.targetAnimator = null;
    this.animController = null;
    this.animator = null;

    this.onAnimClip = null;
    this.offAnimClip = null;

    this.paramName = "";
    this.menuName = "";

    this.makeMenu = false;
  }

  private void TransitionReset(AnimatorStateTransition transition)
  {
    transition.duration = 0;
    transition.offset = 0;
    transition.exitTime = 0;
    transition.hasExitTime = false;
    transition.hasFixedDuration = false;
  }

  private bool SetErrorField(string txt, bool err = false)
  {
    this.errorMsg = txt;
    this.Repaint();
    return err;
  }
}