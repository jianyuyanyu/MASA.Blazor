﻿---
title: Date time pickers（日期时间选择器）
desc: "**PDateTimePicker** 是一个的日期时间选择组件。"
tag: "预置"
related:
  - /blazor/components/date-pickers
  - /blazor/components/time-pickers
  - /blazor/components/date-digital-clock-pickers
---

## 使用 {#usage}

<masa-example file="Examples.components.date_time_pickers.Picker"></masa-example>

通过 `DateTimePickerViewType` 枚举设置选择器视图的呈现方式：

| 枚举项     | 描述                                     |
|---------|----------------------------------------|
| `Auto`    | 根据 Mobile 断点自动切换为 Desktop 或 Mobile 的视图 |
| `Compact` | 总是显示紧凑的视图，但根据 Mobile 断点自动使用菜单或对话框弹出    |
| `Dialog`  | 总是使用对话框弹出，但根据 Mobile 断点自动选择是否使用紧凑视图    |
| `Desktop` | 桌面视图，使用非紧凑视图并使用菜单弹出                    |
| `Mobile`  | 移动端视图，使用紧凑视图并使用对话框弹出                   |

## 示例 {#examples}

### 配置默认触发器 {#default-activator released-on=v1.7.0}

默认使用一个 [MTextField](/blazor/components/text-fields) 作为触发器。可以通过 **PDefaultDateTimePickerActivator** 配置默认触发器。

<masa-example file="Examples.components.date_time_pickers.DefaultActivator"></masa-example>

### 自定义触发器 {#custom-activator}

使用 `ActivatorContent` 插槽自定义触发器。

<masa-example file="Examples.components.date_time_pickers.CustomActivator"></masa-example>

## 视图组件 {#view-components}

### DateTimePickerView

<masa-example file="Examples.components.date_time_pickers.Default"></masa-example>

### DateTimeCompactPickerView

<masa-example file="Examples.components.date_time_pickers.Compact"></masa-example>
