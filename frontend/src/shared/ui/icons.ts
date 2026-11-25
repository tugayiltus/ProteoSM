// Every icon the UI uses, imported by name so only these ship.
import type { IconName, IconType } from '@astryxdesign/core/Icon';
import {
  ArrowLeft, Bell, BookOpen, Bug, Building2, ChartPie, ClipboardList, Contrast, Download, Gauge, GitCompareArrows, History, House, KeyRound, Inbox, Laptop, Layers, List, ListChecks,
  Lock, Mail, Megaphone, MailOpen, MapPin, Monitor, MonitorSmartphone, Moon, NotebookText, Pause, Pencil, Plus, Package, Printer, Settings, Sun, Tag, Tags, User,
  Users, UsersRound,
} from 'lucide-react';

import { BrandMark } from '../brand/BrandMark';

/**
 * App icons for Astryx components (ADR-0027): Astryx semantic names where one exists, so the theme can restyle
 * them, else Lucide glyphs (the neutral theme's own set). One concept, one icon across the app.
 */
export const Icons = {
  arrowLeft: ArrowLeft,
  bell: Bell,
  book: BookOpen,
  brand: BrandMark,
  building: Building2,
  calendar: 'calendar',
  changes: GitCompareArrows,
  chartPie: ChartPie,
  check: 'check',
  checkCircle: 'success',
  close: 'close',
  contrast: Contrast,
  devices: MonitorSmartphone,
  download: Download,
  envelopeClosed: Mail,
  envelopeOpen: MailOpen,
  group: Users,
  history: History,
  home: House,
  inbox: Inbox,
  laptop: Laptop,
  licences: KeyRound,
  list: List,
  listChecks: ListChecks,
  locationPin: MapPin,
  lock: Lock,
  megaphone: Megaphone,
  menu: 'menu',
  moon: Moon,
  notes: NotebookText,
  pause: Pause,
  pencil: Pencil,
  printer: Printer,
  problems: Bug,
  people: UsersRound,
  plus: Plus,
  screenDesktop: Monitor,
  services: Layers,
  releases: Package,
  search: 'search',
  settings: Settings,
  speedometer: Gauge,
  sun: Sun,
  tag: Tag,
  tags: Tags,
  task: ClipboardList,
  timer: 'clock',
  upload: 'upload',
  user: User,
  warning: 'warning',
} satisfies Record<string, IconName | IconType>;

export type AppIcon = keyof typeof Icons;
