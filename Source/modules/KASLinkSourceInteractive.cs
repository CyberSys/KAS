﻿// Kerbal Attachment System
// Author: igor.zavoychinskiy@gmail.com
// License: Public Domain

using KASAPIv2;
using KSPDev.GUIUtils;
using KSPDev.GUIUtils.TypeFormatters;
using KSPDev.PartUtils;
using KSPDev.ProcessingUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace KAS {

/// <summary>Module that allows connecting the parts by a mouse via GUI.</summary>
/// <remarks>
/// When the player starts the linking mode, he must either complete it by clicking on a compatible
/// target part or abort the mode altogether.
/// <para>
/// EVA kerbal movement is locked when linking mode is active, so both source and target parts
/// must be in the range from the kerbal.
/// </para>
/// </remarks>
// Next localization ID: #kasLOC_01003.
public sealed class KASLinkSourceInteractive : KASLinkSourceBase {

  #region Localizable GUI strings
  /// <include file="SpecialDocTags.xml" path="Tags/Message1/*"/>
  /// <include file="KSPDevUtilsAPI_HelpIndex.xml" path="//item[@name='T:KSPDev.GUIUtils.DistanceType']/*"/>
  static readonly Message<DistanceType> CanBeConnectedMsg = new Message<DistanceType>(
      "#kasLOC_01000",
      defaultTemplate: "Click to establish a link (length <<1>>)",
      description: "The message to display when a compatible target part is hovered over, and the"
      + " source is in the linking mode."
      + "\nArgument <<1>> is the possible link length of type DistanceType.",
      example: "Click to establish a link (length 1.22 m)");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message LinkingInProgressMsg = new Message(
      "#kasLOC_01001",
      defaultTemplate: "Select a compatible socket or press ESC",
      description: "The message to display as a help string when an interactive linking mode has"
      + " started.");

  /// <include file="SpecialDocTags.xml" path="Tags/Message0/*"/>
  static readonly Message CannotDockMsg = new Message(
      "#kasLOC_01002",
      defaultTemplate: "Cannot dock: the mode is not supported",
      description: "The message to present when the player requests a docking mode for the link via"
      + " UI, but the source or target part is rejecting the action.");
  #endregion

  #region Part's config fields
  /// <summary>Audio sample to play when the parts are attached by the player.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - plug")]
  public string sndPathPlug = "";

  /// <summary>Audio sample to play when the parts are detached by the player.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - unplug")]
  public string sndPathUnplug = "";

  /// <summary>Audio sample to play when the link is broken by the physics events.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Sound - broke")]
  public string sndPathBroke = "";

  /// <summary>Name of the menu item to start linking mode.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Start link menu text")]
  public string startLinkMenu = "";

  /// <summary>Name of the menu item to break currently established link.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/ConfigSetting/*"/>
  [KSPField]
  [Debug.KASDebugAdjustable("Break link menu text")]
  public string breakLinkMenu = "";
  #endregion

  #region Context menu events/actions
  /// <summary>Event handler. Initiates a link that must be completed by a mouse click.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActiveUnfocused = true)]
  [LocalizableItem(tag = null)]
  public void StartLinkContextMenuAction() {
    StartLinking(GUILinkMode.Interactive, LinkActorType.Player);
  }

  /// <summary>Event handler. Breaks current link between source and target.</summary>
  /// <include file="SpecialDocTags.xml" path="Tags/KspEvent/*"/>
  [KSPEvent(guiActiveUnfocused = true)]
  [LocalizableItem(tag = null)]
  public void BreakLinkContextMenuAction() {
    BreakCurrentLink(LinkActorType.Player);
  }
  #endregion

  #region Local properties and fields
  /// <summary>Color of the pipe in the linking mode when the link can be established.</summary>
  static readonly Color GoodLinkColor = new Color(0, 1, 0, 0.5f);

  /// <summary>Color of the pipe in the linking mode when the link cannot be established.</summary>
  static readonly Color BadLinkColor = new Color(1, 0, 0, 0.5f);

  /// <summary>The lock name that restricts anything but the camera positioning.</summary>
  const string TotalControlLock = "KASInteractiveJointUberLock";

  /// <summary>Shader that reders the pipe during linking.</summary>
  const string InteractiveShaderName = "Transparent/Diffuse";  

  /// <summary>The compatible target under the mouse cursor.</summary>
  ILinkTarget targetCandidate;

  /// <summary>Tells if the connection with the candidate will be successful.</summary>
  bool targetCandidateIsGood;

  /// <summary>
  /// The last known hovered part. Used to trigger the detection of the target candidate.
  /// </summary>
  Part lastHoveredPart;

  /// <summary>The message, displayed during the interactive linking.</summary>
  ScreenMessage statusScreenMessage;

  /// <summary>
  /// A variable to store the auto save state before starting the interactive mode.
  /// </summary>
  bool canAutoSaveState;
  #endregion

  #region KASLinkSourceBase overrides
  /// <inheritdoc/>
  public override void OnAwake() {
    base.OnAwake();
    RegisterGameEventListener(GameEvents.onVesselChange, OnVesselChange);
  }

  /// <inheritdoc/>
  public override void OnUpdate() {
    base.OnUpdate();
    if (linkState == LinkState.Linking && guiLinkMode == GUILinkMode.Interactive) {
      UpdateLinkingState();

      // Handle link mode cancel.
      if (Input.GetKeyUp(KeyCode.Escape)) {
        AsyncCall.CallOnEndOfFrame(this, CancelLinking);
      }
      // Handle link action (mouse click).
      if (Input.GetKeyDown(KeyCode.Mouse0)) {
        if (targetCandidateIsGood ) {
          AsyncCall.CallOnEndOfFrame(this, () => LinkToTarget(targetCandidate));
        } else {
          UISoundPlayer.instance.Play(KASAPI.CommonConfig.sndPathBipWrong);
        }
      }
    }
  }

  /// <inheritdoc/>
  public override void OnStart(StartState state) {
    base.OnStart(state);
    // Infinity duration doesn't mean the message will be shown forever. It must be refreshed in the
    // Update method.
    statusScreenMessage = new ScreenMessage("", Mathf.Infinity, ScreenMessageStyle.UPPER_CENTER);
    UpdateContextMenu();
  }

  /// <inheritdoc/>
  public override void UpdateContextMenu() {
    base.UpdateContextMenu();
    PartModuleUtils.SetupEvent(this, StartLinkContextMenuAction, e => {
                                 e.guiName = startLinkMenu;
                                 e.active = linkState == LinkState.Available;
                               });
    PartModuleUtils.SetupEvent(this, BreakLinkContextMenuAction, e => {
                                 e.guiName = breakLinkMenu;
                                 e.active = linkState == LinkState.Linked;
                               });
  }

  /// <inheritdoc/>
  public override bool StartLinking(GUILinkMode mode, LinkActorType actor) {
    // Don't allow EVA linking mode.
    if (mode != GUILinkMode.Interactive && mode != GUILinkMode.API) {
      return false;
    }
    return base.StartLinking(mode, actor);
  }

  /// <inheritdoc/>
  protected override void SetupStateMachine() {
    base.SetupStateMachine();
    linkStateMachine.onAfterTransition += (start, end) => UpdateContextMenu();
    linkStateMachine.AddStateHandlers(
        LinkState.Linking,
        enterHandler: x => {
          InputLockManager.SetControlLock(
              ControlTypes.All & ~ControlTypes.CAMERACONTROLS, TotalControlLock);
          canAutoSaveState = HighLogic.CurrentGame.Parameters.Flight.CanAutoSave;
          HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = false;
          linkRenderer.shaderNameOverride = InteractiveShaderName;
          linkRenderer.colorOverride = BadLinkColor;
          linkRenderer.isPhysicalCollider = false;
        },
        leaveHandler: x => {
          linkRenderer.StopRenderer();  // This resets the pipe state.
          linkRenderer.shaderNameOverride = null;
          linkRenderer.colorOverride = null;
          linkRenderer.isPhysicalCollider = true;
          ScreenMessages.RemoveMessage(statusScreenMessage);
          InputLockManager.RemoveControlLock(TotalControlLock);
          HighLogic.CurrentGame.Parameters.Flight.CanAutoSave = canAutoSaveState;
          lastHoveredPart = null;
        });
    linkStateMachine.AddStateHandlers(
        LinkState.Linked,
        enterHandler: x => {
          if (linkActor == LinkActorType.Player || linkActor == LinkActorType.Physics) {
            UISoundPlayer.instance.Play(linkJoint.coupleOnLinkMode ? sndPathDock : sndPathPlug);
          }
          var module = linkTarget as PartModule;
          PartModuleUtils.InjectEvent(this, BreakLinkContextMenuAction, module);
        },
        leaveHandler: x => {
          if (linkActor == LinkActorType.Player) {
            UISoundPlayer.instance.Play(linkJoint.coupleOnLinkMode ? sndPathUndock : sndPathUnplug);
          } else if (linkActor == LinkActorType.Physics) {
            UISoundPlayer.instance.Play(sndPathBroke);
          }
          var module = linkTarget as PartModule;
          PartModuleUtils.WithdrawEvent(this, BreakLinkContextMenuAction, module);
        });
  }
  #endregion

  #region Local utility methods
  /// <summary>Displays linking status in real time.</summary>
  void UpdateLinkingState() {
    // Catch the hovered part, a possible target on it, and the link feasibility.
    if (Mouse.HoveredPart != lastHoveredPart) {
      lastHoveredPart = Mouse.HoveredPart;
      targetCandidateIsGood = false;
      if (lastHoveredPart == null ) {
        targetCandidate = null;
      } else {
        targetCandidate = lastHoveredPart.Modules.OfType<ILinkTarget>()
            .FirstOrDefault(x => x.cfgLinkType == cfgLinkType
                            && x.linkState == LinkState.AcceptingLinks);
        if (targetCandidate != null) {
          var linkStatusErrors = new List<string>()
              .Concat(CheckBasicLinkConditions(targetCandidate, checkStates: true))
              .Concat(linkRenderer.CheckColliderHits(nodeTransform, targetCandidate.nodeTransform))
              .Concat(linkJoint.CheckConstraints(this, targetCandidate))
              .ToArray();
          if (linkStatusErrors.Length == 0) {
            targetCandidateIsGood = true;
            statusScreenMessage.message = CanBeConnectedMsg.Format(
                Vector3.Distance(nodeTransform.position, targetCandidate.nodeTransform.position));
          } else {
            statusScreenMessage.message = ScreenMessaging.SetColorToRichText(
                String.Join("\n", linkStatusErrors), ScreenMessaging.ErrorColor);
          }
        }
      }
      // Show the possible link or indicate the error.
      if (targetCandidate != null) {
        linkRenderer.colorOverride = targetCandidateIsGood ? GoodLinkColor : BadLinkColor;
        linkRenderer.StartRenderer(nodeTransform, targetCandidate.nodeTransform);
      } else {
        linkRenderer.colorOverride = BadLinkColor;
        linkRenderer.StopRenderer();
      }
    }

    // Update linking messages (it needs to be refreshed to not go out by timeout).
    if (targetCandidate == null) {
      statusScreenMessage.message = LinkingInProgressMsg;
    }
    ScreenMessages.PostScreenMessage(statusScreenMessage);
  }

  /// <summary>Helper method to execute context menu updates on vessel switch.</summary>
  /// <param name="v">The new active vessel.</param>
  void OnVesselChange(Vessel v) {
    UpdateContextMenu();
    MonoUtilities.RefreshContextWindows(part);
  }
  #endregion
}

}  // namespace
