USE [TIMELAPS]
GO
/****** Object:  Table [dbo].[Attendances]    Script Date: 5/21/2025 10:21:36 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Attendances](
	[AttendanceID] [int] IDENTITY(1,1) NOT NULL,
	[EmployeeID] [int] NOT NULL,
	[DeviceID] [int] NULL,
	[ShiftID] [int] NOT NULL,
	[CheckIn] [datetime] NOT NULL,
	[CheckOut] [datetime] NULL,
	[WorkDate] [date] NOT NULL,
	[Notes] [nvarchar](200) NULL,
PRIMARY KEY CLUSTERED 
(
	[AttendanceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Devices]    Script Date: 5/21/2025 10:21:36 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Devices](
	[DeviceID] [int] IDENTITY(1,1) NOT NULL,
	[MacAddress] [varchar](50) NOT NULL,
	[Description] [nvarchar](200) NULL,
	[IsActive] [bit] NULL,
PRIMARY KEY CLUSTERED 
(
	[DeviceID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[Employees]    Script Date: 5/21/2025 10:21:36 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Employees](
	[EmployeeID] [int] IDENTITY(1,1) NOT NULL,
	[FullName] [nvarchar](100) NOT NULL,
	[Gender] [nvarchar](10) NULL,
	[DateOfBirth] [date] NULL,
	[Department] [nvarchar](100) NULL,
	[Position] [nvarchar](100) NULL,
	[Phone] [nvarchar](20) NULL,
	[Email] [nvarchar](100) NULL,
	[HireDate] [date] NULL,
	[IsActive] [bit] NULL,
PRIMARY KEY CLUSTERED 
(
	[EmployeeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
/****** Object:  Table [dbo].[LeaveTypes]    Script Date: 5/21/2025 10:21:36 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[LeaveTypes](
	[LeaveTypeID] [int] IDENTITY(1,1) NOT NULL,
	[LeaveTypeName] [nvarchar](100) NULL,
	[Description] [nvarchar](200) NULL,
PRIMARY KEY CLUSTERED 
(
	[LeaveTypeID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
SET IDENTITY_INSERT [dbo].[Attendances] ON 

INSERT [dbo].[Attendances] ([AttendanceID], [EmployeeID], [DeviceID], [ShiftID], [CheckIn], [CheckOut], [WorkDate], [Notes]) VALUES (1, 1, 1002, 0, CAST(N'2025-05-21T08:17:46.263' AS DateTime), CAST(N'2025-05-21T08:28:17.253' AS DateTime), CAST(N'2025-05-21' AS Date), N'CheckOut')
SET IDENTITY_INSERT [dbo].[Attendances] OFF
GO
SET IDENTITY_INSERT [dbo].[Devices] ON 

INSERT [dbo].[Devices] ([DeviceID], [MacAddress], [Description], [IsActive]) VALUES (1, N'IPhone 15 Promax', NULL, 1)
INSERT [dbo].[Devices] ([DeviceID], [MacAddress], [Description], [IsActive]) VALUES (2, N'Redmi Note 11 Pro 5G', NULL, 1)
INSERT [dbo].[Devices] ([DeviceID], [MacAddress], [Description], [IsActive]) VALUES (1002, N'30:4f:75:39:cb:d1', NULL, 1)
SET IDENTITY_INSERT [dbo].[Devices] OFF
GO
SET IDENTITY_INSERT [dbo].[Employees] ON 

INSERT [dbo].[Employees] ([EmployeeID], [FullName], [Gender], [DateOfBirth], [Department], [Position], [Phone], [Email], [HireDate], [IsActive]) VALUES (1, N'Lưu Anh', N'Nam', CAST(N'1995-05-14' AS Date), N'Phòng Kỹ thuật', N'Nhân viên IT', N'0912345678', N'luuanh45123@gmail.com', CAST(N'2025-05-14' AS Date), 1)
SET IDENTITY_INSERT [dbo].[Employees] OFF
GO
SET ANSI_PADDING ON
GO
/****** Object:  Index [UQ__Devices__50EDF1CD873A948D]    Script Date: 5/21/2025 10:21:36 AM ******/
ALTER TABLE [dbo].[Devices] ADD UNIQUE NONCLUSTERED 
(
	[MacAddress] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
GO
ALTER TABLE [dbo].[Devices] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Employees] ADD  DEFAULT ((1)) FOR [IsActive]
GO
ALTER TABLE [dbo].[Attendances]  WITH CHECK ADD  CONSTRAINT [FK_Attendances_Devices] FOREIGN KEY([DeviceID])
REFERENCES [dbo].[Devices] ([DeviceID])
GO
ALTER TABLE [dbo].[Attendances] CHECK CONSTRAINT [FK_Attendances_Devices]
GO
ALTER TABLE [dbo].[Attendances]  WITH CHECK ADD  CONSTRAINT [FK_Attendances_Employees] FOREIGN KEY([EmployeeID])
REFERENCES [dbo].[Employees] ([EmployeeID])
GO
ALTER TABLE [dbo].[Attendances] CHECK CONSTRAINT [FK_Attendances_Employees]
GO
