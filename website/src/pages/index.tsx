import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import {
  Rocket,
  LockKeyhole,
  Globe,
  ShieldCheck,
  Layers,
  Wrench,
  Link2,
  Radio,
  type LucideIcon,
} from 'lucide-react';

import styles from './index.module.css';

const features: { icon: LucideIcon; title: string; description: string }[] = [
  {
    icon: Rocket,
    title: 'Native Nomad Integration',
    description:
      'Deploy IIS web applications directly via HashiCorp Nomad job specs — no extra tooling, no custom scripts.',
  },
  {
    icon: LockKeyhole,
    title: 'HTTPS & TLS Certificates',
    description:
      'Supports HTTPS out of the box. Use pre-installed certificates or have Nomad IIS manage them automatically.',
  },
  {
    icon: Globe,
    title: 'Environment Variables',
    description:
      'Pass environment variables straight from your Nomad task stanza into the IIS Application Pool.',
  },
  {
    icon: ShieldCheck,
    title: 'Filesystem Isolation',
    description:
      'Each Application Pool runs under a dedicated service account, scoped to its own directories — no cross-task leakage.',
  },
  {
    icon: Layers,
    title: 'Multiple Application Pools',
    description:
      'Run several App Pools side-by-side within a single Nomad allocation for complex, multi-app deployments.',
  },
  {
    icon: Link2,
    title: 'Existing Website Support',
    description:
      'Use pre-existing websites, making brownfield migrations painless.',
  },
  {
    icon: Radio,
    title: 'Signals & Lifecycle',
    description:
      'Full support for Nomad signals for starting, stopping and recycling IIS app pools on demand.',
  },
  {
    icon: Wrench,
    title: 'Management API',
    description:
      'Powerful REST API for external tooling to inspect, control, and manage allocations beyond standard Nomad capabilities.',
  },
];

function FeatureCard({ icon: Icon, title, description }: { icon: LucideIcon; title: string; description: string }) {
  return (
    <div className={styles.featureCard}>
      <div className={styles.featureIconWrap}>
        <Icon size={22} strokeWidth={1.75} />
      </div>
      <h3 className={styles.featureTitle}>{title}</h3>
      <p className={styles.featureDescription}>{description}</p>
    </div>
  );
}

export default function Home(): JSX.Element {
  const { siteConfig } = useDocusaurusContext();
  return (
    <Layout
      title={siteConfig.title}
      description="A TaskDriver plugin for HashiCorp Nomad to run IIS workloads on Windows.">

      {/* ── Hero ── */}
      <section className={styles.hero}>
        <div className={styles.heroBadge}>Open Source · MIT License</div>
        <img className={styles.heroLogo} alt="Nomad IIS logo" />
        <p className={styles.heroTagline}>{siteConfig.tagline}</p>
        <p className={styles.heroSubtitle}>
          A Nomad task driver written in C# that brings IIS workloads into your
          HashiCorp Nomad cluster — with first-class support for HTTPS,
          multi-app deployments, and filesystem isolation.
        </p>
        <div className={styles.heroActions}>
          <Link className={styles.btnPrimary} to="/docs/getting-started">
            Get Started →
          </Link>
          <Link
            className={styles.btnOutline}
            to="https://github.com/sevensolutions/nomad-iis">
            View on GitHub
          </Link>
        </div>
        <div className={styles.heroSponsor}>
          <iframe
            src="https://github.com/sponsors/sevensolutions/button"
            title="Sponsor sevensolutions"
            height="32"
            width="114"
          />
        </div>
      </section>

      {/* ── Highlights ── */}
      <section className={styles.highlights}>
        <div className={styles.highlightsInner}>
          <h2 className={styles.sectionTitle}>Everything you need to run IIS workloads on Nomad</h2>
          <p className={styles.sectionSubtitle}>
            Nomad IIS bridges the gap between the Nomad scheduler and the
            Windows IIS ecosystem — batteries included.
          </p>
          <div className={styles.featureGrid}>
            {features.map((f) => (
              <FeatureCard key={f.title} {...f} />
            ))}
          </div>
        </div>
      </section>

      {/* ── CTA strip ── */}
      <section className={styles.ctaStrip}>
        <h2>Ready to deploy your first IIS workload?</h2>
        <Link className={styles.btnPrimary} to="/docs/getting-started">
          Read the docs →
        </Link>
      </section>
    </Layout>
  );
}
